namespace TraderEngine.Common.Exchanges;

/// <summary>
/// A shared, do-nothing <see cref="IAsyncDisposable"/>, used wherever a session/lease-style API
/// (e.g. <see cref="IExchangeOrderNotifications.BeginOrderNotificationSessionAsync"/>) needs to
/// return a valid disposable for a no-op case (capability unsupported, or session setup failed)
/// without allocating a new instance each time.
/// </summary>
public sealed class NoOpAsyncDisposable : IAsyncDisposable
{
  public static readonly NoOpAsyncDisposable Instance = new();

  private NoOpAsyncDisposable()
  {
  }

  public ValueTask DisposeAsync()
  {
    return ValueTask.CompletedTask;
  }
}
