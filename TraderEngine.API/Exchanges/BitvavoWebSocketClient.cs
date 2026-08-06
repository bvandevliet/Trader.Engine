using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using TraderEngine.Common.Exchanges;

namespace TraderEngine.API.Exchanges;

/// <summary>
/// Persistent, authenticated WebSocket connection to the Bitvavo v2 streaming API for one set of
/// credentials. Used by <see cref="BitvavoExchange"/> to receive order/fill push updates instead
/// of REST-polling <c>GetOrder</c>. Owned and reused across requests by <see cref="BitvavoWebSocketConnectionPool"/>.
/// </summary>
public sealed class BitvavoWebSocketClient : IAsyncDisposable
{
  private const string WsUrl = "wss://ws.bitvavo.com/v2/";

  private readonly ExchangeCredentials _credentials;
  private readonly ILogger<BitvavoWebSocketClient> _logger;

  private readonly ClientWebSocket _ws = new();
  private readonly CancellationTokenSource _receiveCts = new();
  private Task _receiveLoop = Task.CompletedTask;

  private TaskCompletionSource<bool>? _authTcs;

  // Order/fill push callbacks, keyed by market (e.g. "BTC-EUR").
  private readonly ConcurrentDictionary<string, Action<JsonElement>> _accountCallbacks = new();

  public BitvavoWebSocketClient(ExchangeCredentials credentials, ILogger<BitvavoWebSocketClient> logger)
  {
    _credentials = credentials;
    _logger = logger;
  }

  public async Task ConnectAsync(CancellationToken ct)
  {
    await _ws.ConnectAsync(new Uri(WsUrl), ct);

    _receiveLoop = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token), CancellationToken.None);

    await AuthenticateAsync(ct);
  }

  private async Task AuthenticateAsync(CancellationToken ct)
  {
    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    var signature = BitvavoSignature.Compute(_credentials.ApiSecret, timestamp, "GET", "/v2/websocket", null);

    _authTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

    await SendAsync(new JsonObject
    {
      ["action"] = "authenticate",
      ["key"] = _credentials.ApiKey,
      ["signature"] = signature,
      ["timestamp"] = timestamp,
    }, ct);

    await _authTcs.Task.WaitAsync(ct);
  }

  /// <summary>
  /// Subscribes to order/fill push events for <paramref name="market"/>, replacing any previous
  /// callback registered for that market.
  /// </summary>
  public async Task SubscribeAccountAsync(string market, Action<JsonElement> onEvent, CancellationToken ct)
  {
    _accountCallbacks[market] = onEvent;

    await SendAsync(new JsonObject
    {
      ["action"] = "subscribe",
      ["channels"] = new JsonArray(new JsonObject
      {
        ["name"] = "account",
        ["markets"] = new JsonArray(market),
      }),
    }, ct);
  }

  public void UnsubscribeAccount(string market)
  {
    _accountCallbacks.TryRemove(market, out _);
  }

  private async Task SendAsync(JsonObject message, CancellationToken ct)
  {
    var bytes = Encoding.UTF8.GetBytes(message.ToJsonString());

    await _ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
  }

  private async Task ReceiveLoopAsync(CancellationToken ct)
  {
    var buffer = new byte[64 * 1024];

    try
    {
      while (!ct.IsCancellationRequested && _ws.State == WebSocketState.Open)
      {
        using var messageStream = new MemoryStream();
        WebSocketReceiveResult result;

        do
        {
          result = await _ws.ReceiveAsync(buffer, ct);

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
    await _receiveCts.CancelAsync();

    if (_ws.State == WebSocketState.Open)
    {
      try
      {
        await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnect", CancellationToken.None);
      }
      catch
      {
        // Best-effort close; the socket is being torn down regardless.
      }
    }

    try { await _receiveLoop; } catch (OperationCanceledException) { }

    _ws.Dispose();
    _receiveCts.Dispose();
  }
}
