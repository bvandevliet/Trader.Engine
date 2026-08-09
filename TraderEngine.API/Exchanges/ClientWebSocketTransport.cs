using System.Net.WebSockets;

namespace TraderEngine.API.Exchanges;

/// <summary>
/// Real <see cref="IWebSocketTransport"/> implementation, wrapping a <see cref="ClientWebSocket"/>.
/// </summary>
internal sealed class ClientWebSocketTransport : IWebSocketTransport
{
  private readonly ClientWebSocket _ws = new();

  public WebSocketState State => _ws.State;

  public Task ConnectAsync(Uri uri, CancellationToken ct)
  {
    return _ws.ConnectAsync(uri, ct);
  }

  public Task SendAsync(byte[] data, CancellationToken ct)
  {
    return _ws.SendAsync(data, WebSocketMessageType.Text, endOfMessage: true, ct);
  }

  public Task<WebSocketReceiveResult> ReceiveAsync(byte[] buffer, CancellationToken ct)
  {
    return _ws.ReceiveAsync(buffer, ct);
  }

  public Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken ct)
  {
    return _ws.CloseAsync(closeStatus, statusDescription, ct);
  }

  public ValueTask DisposeAsync()
  {
    _ws.Dispose();

    return ValueTask.CompletedTask;
  }
}
