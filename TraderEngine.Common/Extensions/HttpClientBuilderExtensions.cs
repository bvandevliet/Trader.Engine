using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Contrib.WaitAndRetry;

namespace TraderEngine.Common.Extensions;

public static class HttpClientBuilderExtensions
{
  public static IHttpClientBuilder ApplyDefaultPoolAndPolicyConfig(this IHttpClientBuilder clientBuilder)
  {
    clientBuilder
      .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
      {
        UseCookies = false,
        UseProxy = false,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
      })
      .SetHandlerLifetime(Timeout.InfiniteTimeSpan)
      .AddTransientHttpErrorPolicy(policy =>
        policy
          .OrResult(msg => ((int)msg.StatusCode) >= 500)
          .WaitAndRetryAsync(Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(1), 4)));

    return clientBuilder;
  }
}
