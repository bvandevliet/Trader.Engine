using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace TraderEngine.Common.Extensions;

public static class HttpClientBuilderExtensions
{
  public static IHttpClientBuilder ApplyDefaultPoolAndPolicyConfig(this IHttpClientBuilder clientBuilder)
  {
    clientBuilder
      .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
      {
        // Must stay comfortably under AttemptTimeout (10s default, set below via
        // AddStandardResilienceHandler) — that strategy wraps each attempt's entire duration,
        // connect phase included, so a ConnectTimeout at or above it can never actually be the one
        // that fires; AttemptTimeout would always cancel a hung connect first.
        ConnectTimeout = TimeSpan.FromSeconds(5),
        UseCookies = false,
        UseProxy = false,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
      })
      .SetHandlerLifetime(Timeout.InfiniteTimeSpan)
      .ConfigureHttpClient(httpClient => httpClient.Timeout = TimeSpan.FromSeconds(90))
      // The documented standard stack (outermost to innermost): rate limiter -> total request
      // timeout -> retry -> circuit breaker -> attempt timeout. Each client gets its own pipeline
      // instance (own circuit-breaker/rate-limiter state) built from this shared config, rather
      // than one instance reused across clients — sharing the instance itself would leak
      // circuit-breaker state between unrelated downstream APIs (e.g. a CMC outage would trip the
      // breaker for exchange trading calls too).
      .AddStandardResilienceHandler(options =>
      {
        options.Retry.MaxRetryAttempts = 4;
        options.Retry.Delay = TimeSpan.FromSeconds(1);
        options.Retry.DisableForUnsafeHttpMethods();

        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.FailureRatio = 0.33;
        options.CircuitBreaker.MinimumThroughput = 10;

        // Must comfortably exceed the worst-case retry sequence (4 attempts x up to the 10s
        // default AttemptTimeout, plus backoff delays, ~= 47s) — otherwise the total-timeout
        // strategy aborts the whole operation before all retries get a chance to run.
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(60);
      });

    return clientBuilder;
  }
}
