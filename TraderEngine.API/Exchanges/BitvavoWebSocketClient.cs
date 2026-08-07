using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TraderEngine.Common.Exchanges;

namespace TraderEngine.API.Exchanges;

/// <summary>
/// Persistent, authenticated WebSocket connection to the Bitvavo v2 streaming API for one set of
/// credentials. Used by <see cref="BitvavoExchange"/> to receive order/fill push updates instead
/// of REST-polling <c>GetOrder</c>. Owned and reused across requests by <see cref="BitvavoWebSocketConnectionPool"/>.
///
/// Reconnects automatically (bounded exponential backoff, re-authenticating and re-subscribing
/// every previously active market) on an unexpected close or receive-loop failure. Once the
/// reconnect budget is exhausted, <see cref="IsHealthy"/> becomes false and stays false for the
/// rest of this instance's lifetime; the pool is responsible for discarding a degraded instance
/// and creating a fresh one, this instance never resurrects itself.
/// </summary>
public sealed class BitvavoWebSocketClient : IAsyncDisposable
{
  private const string WsUrl = "wss://ws.bitvavo.com/v2/";

  private static readonly TimeSpan InitialBackoff = TimeSpan.FromMilliseconds(250);
  private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(10);
  private static readonly TimeSpan MaxTotalElapsed = TimeSpan.FromSeconds(30);
  private const int MaxAttempts = 5;

  private readonly ExchangeCredentials _credentials;
  private readonly ILogger<BitvavoWebSocketClient> _logger;
  private readonly BitvavoRateLimitState _rateLimitState;
  private readonly Func<TimeSpan, CancellationToken, Task> _delayFn;
  private readonly Func<IWebSocketTransport> _transportFactory;

  private IWebSocketTransport _transport = null!;

  // Guards _transport (and the receive loop it owns) against concurrent replacement during a
  // reconnect. Sends only hold this briefly to read the current transport reference, not for
  // their whole duration; the reconnect routine holds it for its entire attempt sequence.
  private readonly SemaphoreSlim _connectionLock = new(1, 1);

  private readonly CancellationTokenSource _disposeCts = new();
  private volatile bool _disposing;
  private volatile bool _gaveUp;

  private Task _receiveLoop = Task.CompletedTask;

  private TaskCompletionSource<bool>? _authTcs;

  // Order/fill push callbacks, keyed by market (e.g. "BTC-EUR"). Re-sent as subscribe messages
  // for every entry here after a successful reconnect.
  private readonly ConcurrentDictionary<string, Action<JsonElement>> _accountCallbacks = new();

  /// <summary>
  /// True once connected and authenticated, remaining true across transient reconnect attempts;
  /// becomes false, permanently, only once the reconnect budget is fully exhausted or this client
  /// is disposed. The connection pool should discard (not reuse) a client once this is false.
  /// </summary>
  public bool IsHealthy => !_disposing && !_gaveUp;

  public BitvavoWebSocketClient(
    ExchangeCredentials credentials,
    ILogger<BitvavoWebSocketClient> logger,
    BitvavoRateLimitState rateLimitState,
    Func<TimeSpan, CancellationToken, Task>? delayFn = null,
    Func<IWebSocketTransport>? transportFactory = null)
  {
    _credentials = credentials;
    _logger = logger;
    _rateLimitState = rateLimitState;
    _delayFn = delayFn ?? Task.Delay;
    _transportFactory = transportFactory ?? (() => new ClientWebSocketTransport());
  }

  public async Task ConnectAsync(CancellationToken ct)
  {
    await _connectionLock.WaitAsync(ct);
    try
    {
      await ConnectAndAuthenticateAsync(ct);
    }
    finally
    {
      _connectionLock.Release();
    }
  }

  /// <summary>
  /// Connects a fresh transport, starts its receive loop, and authenticates. Caller must hold
  /// <see cref="_connectionLock"/>.
  /// </summary>
  private async Task ConnectAndAuthenticateAsync(CancellationToken ct)
  {
    var transport = _transportFactory();

    await transport.ConnectAsync(new Uri(WsUrl), ct);

    _transport = transport;
    _receiveLoop = Task.Run(() => ReceiveLoopAsync(transport, _disposeCts.Token), CancellationToken.None);

    await AuthenticateAsync(transport, ct);
  }

  private async Task AuthenticateAsync(IWebSocketTransport transport, CancellationToken ct)
  {
    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    var signature = BitvavoSignature.Compute(_credentials.ApiSecret, timestamp, "GET", "/v2/websocket", null);

    _authTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

    await SendRawAsync(transport, new JsonObject
    {
      ["action"] = "authenticate",
      ["key"] = _credentials.ApiKey,
      ["signature"] = signature,
      ["timestamp"] = timestamp,
      ["window"] = BitvavoDefaults.AccessWindowMs,
    }, ct);

    await _authTcs.Task.WaitAsync(ct);
  }

  /// <summary>
  /// Subscribes to order/fill push events for <paramref name="market"/>, replacing any previous
  /// callback registered for that market. Re-sent automatically after a reconnect.
  /// </summary>
  public async Task SubscribeAccountAsync(string market, Action<JsonElement> onEvent, CancellationToken ct)
  {
    _accountCallbacks[market] = onEvent;

    await SendAsync(BuildSubscribeMessage(market), ct);
  }

  public void UnsubscribeAccount(string market)
  {
    _accountCallbacks.TryRemove(market, out _);
  }

  private static JsonObject BuildSubscribeMessage(string market)
  {
    return new JsonObject
    {
      ["action"] = "subscribe",
      ["channels"] = new JsonArray(new JsonObject
      {
        ["name"] = "account",
        ["markets"] = new JsonArray(market),
      }),
    };
  }

  private async Task ResubscribeAllAsync(IWebSocketTransport transport, CancellationToken ct)
  {
    foreach (var market in _accountCallbacks.Keys)
      await SendRawAsync(transport, BuildSubscribeMessage(market), ct);
  }

  private async Task SendAsync(JsonObject message, CancellationToken ct)
  {
    IWebSocketTransport transport;

    await _connectionLock.WaitAsync(ct);
    try
    {
      transport = _transport;
    }
    finally
    {
      _connectionLock.Release();
    }

    await SendRawAsync(transport, message, ct);
  }

  private static async Task SendRawAsync(IWebSocketTransport transport, JsonObject message, CancellationToken ct)
  {
    var bytes = Encoding.UTF8.GetBytes(message.ToJsonString());

    await transport.SendAsync(bytes, ct);
  }

  private async Task ReceiveLoopAsync(IWebSocketTransport transport, CancellationToken ct)
  {
    var buffer = new byte[64 * 1024];

    try
    {
      while (!ct.IsCancellationRequested && transport.State == WebSocketState.Open)
      {
        using var messageStream = new MemoryStream();
        WebSocketReceiveResult result;

        do
        {
          result = await transport.ReceiveAsync(buffer, ct);

          if (result.MessageType == WebSocketMessageType.Close)
          {
            _logger.LogWarning("Bitvavo WebSocket closed by server: {Status}", result.CloseStatusDescription);
            return;
          }

          messageStream.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        messageStream.Position = 0;

        try
        {
          var element = JsonDocument.Parse(messageStream).RootElement;
          Dispatch(element);
        }
        catch (JsonException ex)
        {
          _logger.LogError(ex, "Failed to parse Bitvavo WebSocket message.");
        }
      }
    }
    catch (OperationCanceledException) { /* normal shutdown */ }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Bitvavo WebSocket receive loop terminated unexpectedly.");
    }
    finally
    {
      // Any exit that wasn't caused by our own cancellation (disposal) is unexpected and worth
      // trying to recover from — including falling out of the while loop because the transport's
      // State silently stopped being Open, not just an explicit close frame or exception.
      if (!ct.IsCancellationRequested && !_disposing)
        _ = ReconnectLoopAsync(_disposeCts.Token);
    }
  }

  private async Task ReconnectLoopAsync(CancellationToken ct)
  {
    if (_gaveUp || _disposing)
      return;

    try
    {
      await _connectionLock.WaitAsync(ct);
      try
      {
        var delay = InitialBackoff;
        var start = DateTimeOffset.UtcNow;

        for (var attempt = 1; attempt <= MaxAttempts && DateTimeOffset.UtcNow - start < MaxTotalElapsed; attempt++)
        {
          if (_rateLimitState.IsBanned(DateTimeOffset.UtcNow))
          {
            _logger.LogWarning("Bitvavo account appears rate-limited; skipping further WebSocket reconnect attempts.");
            break;
          }

          try
          {
            _logger.LogInformation("Attempting to reconnect Bitvavo WebSocket (attempt {Attempt}/{Max}).", attempt, MaxAttempts);

            await ConnectAndAuthenticateAsync(ct);
            await ResubscribeAllAsync(_transport, ct);

            _logger.LogInformation("Bitvavo WebSocket reconnected successfully.");
            return;
          }
          catch (Exception ex) when (ex is not OperationCanceledException)
          {
            _logger.LogWarning(ex, "Bitvavo WebSocket reconnect attempt {Attempt}/{Max} failed.", attempt, MaxAttempts);
          }

          await _delayFn(delay, ct);
          delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, MaxBackoff.Ticks));
        }

        _gaveUp = true;
        _logger.LogError("Bitvavo WebSocket reconnect exhausted; giving up for this connection.");
      }
      finally
      {
        _connectionLock.Release();
      }
    }
    catch (OperationCanceledException)
    {
      // Disposal requested mid-reconnect; nothing more to do.
    }
  }

  private void Dispatch(JsonElement element)
  {
    if (!element.TryGetProperty("event", out var eventProp))
      return;

    var eventName = eventProp.GetString();

    if (eventName == "authenticate")
    {
      _authTcs?.TrySetResult(true);
      return;
    }

    if (eventName == "error")
    {
      _logger.LogError("Bitvavo WebSocket error: {Message}", element.GetRawText());
      _authTcs?.TrySetException(new InvalidOperationException($"Bitvavo WebSocket error: {element.GetRawText()}"));
      return;
    }

    // Only "order" (status transitions) and "fill" (partial/full trade execution) carry order updates.
    if (eventName != "order" && eventName != "fill")
      return;

    if (!element.TryGetProperty("market", out var marketProp))
      return;

    var market = marketProp.GetString();

    if (market != null && _accountCallbacks.TryGetValue(market, out var callback))
    {
      try
      {
        callback(element);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Bitvavo WebSocket account callback for market {Market} threw.", market);
      }
    }
  }

  public async ValueTask DisposeAsync()
  {
    _disposing = true;

    await _disposeCts.CancelAsync();

    await _connectionLock.WaitAsync();
    try
    {
      if (_transport.State == WebSocketState.Open)
      {
        try
        {
          await _transport.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnect", CancellationToken.None);
        }
        catch
        {
          // Best-effort close; the socket is being torn down regardless.
        }
      }
    }
    finally
    {
      _connectionLock.Release();
    }

    try { await _receiveLoop; } catch (OperationCanceledException) { }

    await _transport.DisposeAsync();
    _connectionLock.Dispose();
    _disposeCts.Dispose();
  }
}
