using Microsoft.AspNetCore.Authorization;
using TraderEngine.Web.Identity;

namespace TraderEngine.Web.Middleware;

/// <summary>
/// Redirects authenticated users to the change-password page when their
/// <see cref="AppClaimTypes.MustChangePassword"/> claim is set. The claim is embedded in the auth
/// cookie by <see cref="UserClaimsPrincipalFactory"/> so no database hit is required per request.
/// </summary>
/// <remarks>
/// The allowed paths are resolved via <see cref="LinkGenerator"/> rather than hardcoded, since
/// this app (unlike the SimplePlanner.Net reference) has no page-route slugify convention — the
/// literal route segment for e.g. "ChangePassword" would otherwise have to be guessed.
/// </remarks>
public class MustChangePasswordMiddleware(RequestDelegate next, LinkGenerator linkGenerator)
{
  private readonly string _changePasswordPath =
    (linkGenerator.GetPathByPage(page: "/Account/Manage/ChangePassword", values: new { area = "Identity" })
      ?? "/identity/account/manage/changepassword").ToLowerInvariant();

  private readonly string _logoutPath =
    (linkGenerator.GetPathByPage(page: "/Account/Logout", values: new { area = "Identity" })
      ?? "/identity/account/logout").ToLowerInvariant();

  public async Task InvokeAsync(HttpContext context)
  {
    if (context.User.Identity?.IsAuthenticated == true
      && context.User.HasClaim(c => c.Type == AppClaimTypes.MustChangePassword && c.Value == bool.TrueString))
    {
      // Skip endpoints that allow anonymous access (e.g. static assets served by MapStaticAssets).
      // Redirecting those would break CSS/JS loading on the change-password page itself.
      var endpoint = context.GetEndpoint();
      if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
      {
        await next(context);
        return;
      }

      var path = context.Request.Path.Value ?? string.Empty;
      var isAllowed = path.StartsWith(_changePasswordPath, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(_logoutPath, StringComparison.OrdinalIgnoreCase);

      if (!isAllowed)
      {
        context.Response.Redirect(_changePasswordPath);
        return;
      }
    }

    await next(context);
  }
}
