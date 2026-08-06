using TraderEngine.Common.DTOs.API.Response;

namespace TraderEngine.Common.Exchanges;

/// <summary>
/// Optional capability for <see cref="IExchange"/> implementations that can push real-time order
/// status updates (e.g. via WebSocket). When an exchange implements this, <see cref="Services.RebalancingService"/>
/// uses it instead of polling <see cref="IExchange.GetOrder"/> on a fixed interval, so fills are
/// detected almost immediately rather than up to a second late.
/// </summary>
public interface IExchangeOrderNotifications
{
  /// <summary>
  /// Waits until <paramref name="order"/> reaches a terminal <see cref="OrderDto.HasEnded"/> status,
  /// or until <paramref name="timeout"/> elapses. Never throws: any connection or protocol failure
  /// is caught internally and reported as a null result so the caller can fall back to REST polling.
  /// </summary>
  /// <returns>The updated, ended order; or null if no confirmed terminal state was observed before <paramref name="timeout"/>.</returns>
  Task<OrderDto?> WaitForOrderEndedAsync(ExchangeCredentials credentials, OrderDto order, TimeSpan timeout, CancellationToken ct = default);
}
