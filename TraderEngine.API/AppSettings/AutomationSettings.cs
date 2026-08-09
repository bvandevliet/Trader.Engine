namespace TraderEngine.API.AppSettings;

/// <summary>
/// Tunes <see cref="Services.AutomationOrchestrator"/>'s per-cycle behavior.
/// </summary>
public class AutomationSettings
{
  /// <summary>
  /// Maximum number of users' automation cycles to run concurrently within a single cycle.
  /// Every user's Bitvavo REST traffic leaves from this app's single outbound IP and shares one
  /// rate-limit budget (see <see cref="Exchanges.BitvavoRateLimitState"/>) regardless of which
  /// account it belongs to, so an unbounded fan-out lets one especially busy cycle throttle every
  /// other user's cycle in the same run at once, instead of degrading gracefully. Defaults to 5.
  /// </summary>
  public int MaxConcurrentRuns { get; set; } = 5;
}
