using Microsoft.AspNetCore.Identity;

namespace TraderEngine.Data.Extensions;

public static class IdentityOptionsExtensions
{
  /// <summary>
  /// Password and lockout policy shared identically by TraderEngine.API and TraderEngine.Web —
  /// both authenticate against the same AppUser store, so a policy configured on only one side
  /// would silently not apply when an account is touched through the other (e.g. a brute-force
  /// attempt against the API's own /api/auth/login getting a shorter lockout window than one
  /// against the Web login form).
  /// </summary>
  public static void ConfigureTraderEngineIdentityPolicy(this IdentityOptions options)
  {
    options.SignIn.RequireConfirmedAccount = false;

    // Password requirements — an operator account here can trigger real exchange trades, so
    // this is deliberately stricter than ASP.NET Core Identity's own defaults (6/5min).
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    options.Password.RequiredUniqueChars = 1;

    // Brute-force protection.
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
  }
}
