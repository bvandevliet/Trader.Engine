using Microsoft.Extensions.DependencyInjection;

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
      .ConfigureHttpClient(httpClient =>
      {
        httpClient.Timeout = TimeSpan.FromSeconds(299);
      });

    return clientBuilder;
  }
}
