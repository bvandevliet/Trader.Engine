using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;

namespace TraderEngine.Data.Extensions;

public static class ForwardedHeadersExtensions
{
  /// <summary>
  /// Trusts X-Forwarded-For/-Proto from the nginx reverse proxy — shared identically by
  /// TraderEngine.API and TraderEngine.Web — so RemoteIpAddress/Request.IsHttps reflect the real
  /// client, not the proxy hop. Trusted hops are restricted to loopback/private-network ranges
  /// rather than requiring a per-deployment KnownProxies/KnownNetworks config: leaving both lists
  /// empty would revert to ForwardedHeadersMiddleware's legacy pre-.NET-8 behavior of trusting
  /// *any* proxy, which would let a client reaching the container directly (e.g. its published
  /// port, bypassing nginx) spoof its own IP/scheme. A hop from a public address is never trusted.
  /// </summary>
  public static IServiceCollection ConfigureTraderEngineForwardedHeaders(this IServiceCollection services)
  {
    return services.Configure<ForwardedHeadersOptions>(options =>
    {
      options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
      options.KnownProxies.Clear();
      options.KnownIPNetworks.Clear();
      options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("127.0.0.0/8"));
      options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("::1/128"));
      options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("10.0.0.0/8"));
      options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("172.16.0.0/12"));
      options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("192.168.0.0/16"));
    });
  }
}
