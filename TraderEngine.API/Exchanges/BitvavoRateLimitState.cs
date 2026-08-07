namespace TraderEngine.API.Exchanges;

/// <summary>
/// Shared, thread-safe view of Bitvavo's REST rate limit, updated from the
/// <c>bitvavo-ratelimit-remaining</c>/<c>bitvavo-ratelimit-resetat</c> response headers (and, as a
/// fallback when those headers are absent from an error response, from an observed ban). Registered
/// as a singleton so all requests against the single Bitvavo <see cref="HttpClient"/> share one view,
/// since the limit itself is account/IP-wide, not per logical call.
/// </summary>
public sealed class BitvavoRateLimitState
{
  // Interlocked rather than a lock: each field is read/written independently and there is no
  // compound invariant across them that needs atomic updating together — worst case under a race
  // is one extra proactive wait or one skipped one, never a correctness issue.
  private long _remaining = -1;
  private long _resetAtUnixMs;

  /// <summary>
  /// Last observed remaining call budget, or -1 if never observed.
  /// </summary>
  public int Remaining => (int)Interlocked.Read(ref _remaining);

  /// <summary>
  /// Last observed rate-limit reset time, or null if never observed.
  /// </summary>
  public DateTimeOffset? ResetAt
  {
    get
    {
      var value = Interlocked.Read(ref _resetAtUnixMs);

      return value == 0 ? null : DateTimeOffset.FromUnixTimeMilliseconds(value);
    }
  }

  /// <summary>
  /// Records the rate-limit headers observed on a response.
  /// </summary>
  public void ObserveHeaders(int remaining, long resetAtUnixMs)
  {
    Interlocked.Exchange(ref _remaining, remaining);
    Interlocked.Exchange(ref _resetAtUnixMs, resetAtUnixMs);
  }

  /// <summary>
  /// Records that Bitvavo reported a rate-limit ban (errorCode 105) without usable headers on that
  /// same response, so a conservative fixed reset time is assumed instead.
  /// </summary>
  public void ObserveBan(DateTimeOffset resetAt)
  {
    Interlocked.Exchange(ref _remaining, 0);
    Interlocked.Exchange(ref _resetAtUnixMs, resetAt.ToUnixTimeMilliseconds());
  }

  /// <summary>
  /// True if the account is currently believed to be rate-limited (banned), i.e. <see cref="Remaining"/>
  /// is known and exhausted and <see cref="ResetAt"/> is known and still in the future.
  /// </summary>
  public bool IsBanned(DateTimeOffset now)
  {
    return Remaining == 0 && ResetAt is { } resetAt && resetAt > now;
  }
}
