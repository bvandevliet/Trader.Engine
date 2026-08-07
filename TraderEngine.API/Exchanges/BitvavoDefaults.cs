namespace TraderEngine.API.Exchanges;

/// <summary>
/// Shared defaults for Bitvavo REST and WebSocket authentication, kept in one place so the two
/// protocols can't drift apart (e.g. one sending a stale/typo'd value the other doesn't).
/// </summary>
internal static class BitvavoDefaults
{
  /// <summary>
  /// The window, in milliseconds, within which a signed request/message must be received by
  /// Bitvavo relative to its timestamp. Sent as the REST <c>bitvavo-access-window</c> header and
  /// the WebSocket <c>authenticate</c> message's <c>window</c> field.
  /// </summary>
  public const int AccessWindowMs = 10000;
}
