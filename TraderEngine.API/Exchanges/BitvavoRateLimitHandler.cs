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
    // it's logged against. Naming the delayed request's own API key (masked; every BitvavoExchange
    // call carries one via CreateRequestMsg's "bitvavo-access-key" header) at least makes clear
    // whose request is currently paying the wait, even though it can't say whose activity drove
    // the shared budget down in the first place.
    _logger.LogInformation(
      "Bitvavo rate limit low ({Remaining} remaining); waiting {Wait} before sending the next request for API key {ApiKey}.",
      _state.Remaining, wait, MaskApiKey(request.Headers.TryGetValues("bitvavo-access-key", out var values) ? values.FirstOrDefault() : null));

    await _delayFn(wait, ct);
  }

  /// <summary>
  /// Reduces an API key down to its last 4 characters for logging, so a diagnostic log line can
  /// still distinguish which account is affected without ever writing a usable secret to the log.
  /// </summary>
  private static string MaskApiKey(string? apiKey)
  {
    return string.IsNullOrEmpty(apiKey)
      ? "unknown"
      : $"...{apiKey[Math.Max(0, apiKey.Length - 4)..]}";
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
