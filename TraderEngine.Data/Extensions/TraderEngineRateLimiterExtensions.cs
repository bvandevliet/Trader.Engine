using Microsoft.AspNetCore.RateLimiting;

namespace TraderEngine.Data.Extensions;

public static class TraderEngineRateLimiterExtensions
{
  public const string LoginPolicy = "login";

  /// <summary>
  /// Throttles login attempts per client IP — shared identically by TraderEngine.API's
  /// AuthController and TraderEngine.Web's Identity login page — as a second layer of
  /// brute-force protection alongside Identity's per-account lockout
  /// (ConfigureTraderEngineIdentityPolicy), which only kicks in once a specific account has
  /// already failed enough times. Applied via <c>[EnableRateLimiting(LoginPolicy)]</c>.
  /// </summary>
  public static RateLimiterOptions AddTraderEngineLoginPolicy(this RateLimiterOptions options)
  {
    options.AddFixedWindowLimiter(LoginPolicy, limiterOptions =>
    {
      limiterOptions.PermitLimit = 5;
      limiterOptions.Window = TimeSpan.FromMinutes(1);
      limiterOptions.QueueLimit = 0;
    });

    return options;
  }
}
