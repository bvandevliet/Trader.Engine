using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using TraderEngine.API.Exchanges;

namespace TraderEngine.API.Tests.Exchanges;

/// <summary>
/// A scriptable <see cref="IWebSocketTransport"/> double, letting tests drive
/// <see cref="BitvavoWebSocketClient"/>'s connect/reconnect/authenticate/resubscribe logic without
/// a real socket. Each instance represents one "connection attempt" — <see cref="ConnectAsync"/>
/// either succeeds (per <see cref="ThrowOnConnect"/>) or throws, mirroring how a real reconnect
/// creates a brand-new transport per attempt.
/// </summary>
internal sealed class FakeWebSocketTransport : IWebSocketTransport
{
  private readonly Channel<(byte[] Data, WebSocketMessageType Type)> _incoming = Channel.CreateUnbounded<(byte[], WebSocketMessageType)>();

  public WebSocketState State { get; private set; } = WebSocketState.Connecting;

  public List<string> SentMessages { get; } = [];

  public bool ThrowOnConnect { get; init; }

  public bool AutoAuthenticate { get; init; } = true;

  public Task ConnectAsync(Uri uri, CancellationToken ct)
  {
    if (ThrowOnConnect)
      throw new InvalidOperationException("Simulated connect failure.");

    State = WebSocketState.Open;

    return Task.CompletedTask;
  }

  public Task SendAsync(byte[] data, CancellationToken ct)
  {
    var json = Encoding.UTF8.GetString(data);

    SentMessages.Add(json);

    // Auto-respond to an authenticate message with a successful "authenticate" event, as the real
    // server would, so BitvavoWebSocketClient's AuthenticateAsync resolves without the test having
    // to script it explicitly for every connect/reconnect attempt.
    if (AutoAuthenticate && json.Contains("\"action\":\"authenticate\""))
      EnqueueMessage("""{"event":"authenticate","authenticated":true}""");

    return Task.CompletedTask;
  }

  public async Task<WebSocketReceiveResult> ReceiveAsync(byte[] buffer, CancellationToken ct)
  {
    var (data, type) = await _incoming.Reader.ReadAsync(ct);

    if (type == WebSocketMessageType.Close)
    {
      State = WebSocketState.Closed;

      return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true, WebSocketCloseStatus.NormalClosure, "closed");
    }

    data.CopyTo(buffer, 0);

    return new WebSocketReceiveResult(data.Length, type, endOfMessage: true);
  }

  public Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken ct)
  {
    State = WebSocketState.Closed;

    _incoming.Writer.TryComplete();

    return Task.CompletedTask;
  }

  public ValueTask DisposeAsync()
  {
    _incoming.Writer.TryComplete();

    return ValueTask.CompletedTask;
  }

  public void EnqueueMessage(string json)
  {
    _incoming.Writer.TryWrite((Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text));
  }

  /// <summary>
  /// Simulates the server closing the connection, triggering <see cref="BitvavoWebSocketClient"/>'s
  /// receive loop to exit and its reconnect logic to kick in.
  /// </summary>
  public void SimulateServerClose()
  {
    _incoming.Writer.TryWrite(([], WebSocketMessageType.Close));
  }
}
