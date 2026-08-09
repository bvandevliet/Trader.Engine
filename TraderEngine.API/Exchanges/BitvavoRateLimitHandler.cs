using System.Net.Http.Headers;

namespace TraderEngine.API.Exchanges;

/// <summary>
/// Proactively self-throttles Bitvavo REST requests based on the <c>bitvavo-ratelimit-remaining</c>/
/// <c>bitvavo-ratelimit-resetat</c> response headers, and, on a rate-limit ban (errorCode 105)
/// without usable headers on that response, falls back to a conservative fixed wait. Registered only
/// on the Bitvavo <see cref="IExchange"/> <see cref="HttpClient"/>, not the shared resilience pipeline
/// (<c>ApplyDefaultPoolAndPolicyConfig</c>), since this behavior is Bitvavo-specific.
/// </summary>
public sealed class BitvavoRateLimitHandler : DelegatingHandler
{
  /// <summary>
  /// Carries this app's own user id on an outgoing request (set by <see cref="BitvavoExchange.CreateRequestMsg"/>
  /// from <see cref="ExchangeCredentials.UserId"/>, when known), purely so the throttle log below
  /// can attribute a delay to a specific user without ever logging exchange credential material.
  /// </summary>
  public static readonly HttpRequestOptionsKey<Guid> UserIdOptionKey = new("TraderEngine.UserId");

  // Bitvavo's default REST budget is 1000 weight/minute; throttling once this low leaves headroom
  // for a handful of in-flight concurrent calls without needing to track per-endpoint weights.
  private const int ThrottleThreshold = 50;

  // Bitvavo's rate-limit window is 60s; anything longer than this is untrustworthy (stale/corrupt
  // ResetAt) and must never be allowed to hang a request indefinitely.
  private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(65);

  private static readonly TimeSpan BanFallbackWait = TimeSpan.FromSeconds(60);

  private readonly BitvavoRateLimitState _state;
  private readonly ILogger<BitvavoRateLimitHandler> _logger;
  private readonly Func<TimeSpan, CancellationToken, Task> _delayFn;

  public BitvavoRateLimitHandler(
    BitvavoRateLimitState state,
    ILogger<BitvavoRateLimitHandler> logger,
    Func<TimeSpan, CancellationToken, Task>? delayFn = null)
  {
    _state = state;
    _logger = logger;
    _delayFn = delayFn ?? Task.Delay;
  }

  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
  {
    await ThrottleIfNeededAsync(request, ct);

    var response = await base.SendAsync(request, ct);

    var headersObserved = ObserveHeaders(response.Headers);

    if (!response.IsSuccessStatusCode)
      await ObserveErrorBodyAsync(response, headersObserved, ct);

    return response;
  }

  private async Task ThrottleIfNeededAsync(HttpRequestMessage request, CancellationToken ct)
  {
    if (_state.Remaining < 0 || _state.Remaining > ThrottleThreshold)
      return;

    if (_state.ResetAt is not { } resetAt)
      return;

    var wait = resetAt - DateTimeOffset.UtcNow;

    if (wait <= TimeSpan.Zero)
      return;

    if (wait > MaxWait)
      wait = MaxWait;

    // The rate limit itself is shared account/IP-wide (see BitvavoRateLimitState's doc comment),
    // so this delay can be caused by — and applies to — a different user's request than the one
    // it's logged against. Preferring the app's own user id (set on the request by BitvavoExchange
    // whenever the caller supplied one) over any fragment of exchange credential material: it's
    // the same identifier every other log line in this app already correlates on, so an operator
    // doesn't need a separate "which user owns this key" lookup mid-incident, and nothing derived
    // from ApiKey/ApiSecret needs to appear in a log line at all.
    _logger.LogInformation(
      "Bitvavo rate limit low ({Remaining} remaining); waiting {Wait} before sending the next request for user {UserId}.",
      _state.Remaining, wait, request.Options.TryGetValue(UserIdOptionKey, out var userId) ? userId : "unknown");

    await _delayFn(wait, ct);
  }

  /// <returns>True if both rate-limit headers were present and parsed successfully.</returns>
  private bool ObserveHeaders(HttpResponseHeaders headers)
  {
    if (!headers.TryGetValues("bitvavo-ratelimit-remaining", out var remainingValues)
      || !int.TryParse(remainingValues.FirstOrDefault(), out var remaining))
      return false;

    if (!headers.TryGetValues("bitvavo-ratelimit-resetat", out var resetAtValues)
      || !long.TryParse(resetAtValues.FirstOrDefault(), out var resetAtUnixMs))
      return false;

    _state.ObserveHeaders(remaining, resetAtUnixMs);

    return true;
  }

  private async Task ObserveErrorBodyAsync(HttpResponseMessage response, bool headersObserved, CancellationToken ct)
  {
    // Buffer (rather than read-and-consume) so the caller's own subsequent
    // response.Content.DeserializeAsync<T>()/ReadAsStringAsync() calls still work unchanged.
    await response.Content.LoadIntoBufferAsync(ct);

    // If usable headers were present on this same response, they already updated the state above
    // with a more precise reset time than the fixed fallback below would give.
    if (headersObserved)
      return;

    var body = await response.Content.ReadAsStringAsync(ct);

    if (!body.Contains("\"errorCode\":105", StringComparison.Ordinal))
      return;

    _logger.LogWarning("Bitvavo reported a rate-limit ban (errorCode 105) with no usable rate-limit headers on the response; assuming a {Wait} ban.", BanFallbackWait);

    _state.ObserveBan(DateTimeOffset.UtcNow + BanFallbackWait);
  }
}
