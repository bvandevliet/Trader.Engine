namespace TraderEngine.API.AppSettings;

/// <summary>
/// Tunes <see cref="TraderEngine.Common.Services.RebalancingService"/>'s order-verification budget.
/// </summary>
public class RebalancingSettings
{
  /// <summary>
  /// How long, in seconds, to wait for an order to end (fill or otherwise) before giving up — for
  /// a <see cref="TraderEngine.Common.Enums.OrderType.Limit"/> order this is the window before it
  /// gets canceled and a market order is placed for any unfilled remainder. Defaults to 60,
  /// matching <see cref="TraderEngine.Common.Services.RebalancingService.VerifyOrderEnded"/>'s
  /// prior hardcoded default.
  /// </summary>
  public int FillWaitTimeoutSeconds { get; set; } = 60;
}
