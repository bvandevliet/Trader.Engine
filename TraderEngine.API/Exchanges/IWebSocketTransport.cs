using System.Net.WebSockets;

namespace TraderEngine.API.Exchanges;

/// <summary>
/// Thin seam over <see cref="ClientWebSocket"/> so <see cref="BitvavoWebSocketClient"/>'s
/// connect/reconnect/backoff logic can be unit tested against a scriptable fake instead of a real
/// socket, mirroring how <see cref="HttpClient"/> is injected into <see cref="BitvavoExchange"/>.
/// </summary>
public interface IWebSocketTransport : IAsyncDisposable
{
  WebSocketState State { get; }

  Task ConnectAsync(Uri uri, CancellationToken ct);

  Task SendAsync(byte[] data, CancellationToken ct);

  Task<WebSocketReceiveResult> ReceiveAsync(byte[] buffer, CancellationToken ct);

  Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken ct);
}
