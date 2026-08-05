using System.Security.Claims;
using System.Threading.RateLimiting;
using TraderEngine.Data.Extensions;

namespace TraderEngine.API.Extensions;

public static class RateLimiterExtensions
{
  /// <summary>
  /// API-only rate limiting: the shared "login" policy (see TraderEngineRateLimiterExtensions,
  /// also applied by TraderEngine.Web's login page) plus a "trading" policy that throttles the
  /// trade-triggering endpoints (Rebalance/Allocations controllers) per authenticated user, so a
  /// leaked/hijacked JWT can't be used to fire off rapid-repeat exchange orders. "trading" is
  /// keyed on the user id claim rather than IP: this app is reached only via TraderEngine.Web's
  /// internal call, so every request already carries a real user identity.
  /// </summary>
  public static IServiceCollection AddTraderEngineApiRateLimiting(this IServiceCollection services)
  {
    return services.AddRateLimiter(options =>
    {
      options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

      options.AddTraderEngineLoginPolicy();

      options.AddPolicy("trading", httpContext =>
      {
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";

        return RateLimitPartition.GetSlidingWindowLimiter(userId, _ => new SlidingWindowRateLimiterOptions
        {
          PermitLimit = 20,
          Window = TimeSpan.FromMinutes(1),
          SegmentsPerWindow = 4,
          QueueLimit = 0,
        });
      });
    });
  }
}
