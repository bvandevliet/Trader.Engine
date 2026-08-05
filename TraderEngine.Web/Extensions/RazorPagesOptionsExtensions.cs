using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TraderEngine.Data.Constants;
using TraderEngine.Web.Middleware;

namespace TraderEngine.Web.Extensions;

public static class RazorPagesOptionsExtensions
{
  /// <summary>
  /// Page routing/authorization conventions for the Identity area and the app's default route.
  /// Razor Pages validate antiforgery tokens on unsafe verbs automatically — no equivalent of
  /// MVC's AutoValidateAntiforgeryTokenAttribute filter registration is needed.
  /// </summary>
  public static void ConfigureTraderEngineRazorPages(this RazorPagesOptions options)
  {
    // Kebab-cases page routes (e.g. AccessDenied -> access-denied) to match the hyphenated
    // paths configured in ConfigureApplicationCookie.
    options.Conventions.Add(new PageRouteTransformerConvention(new SlugifyParameterTransformer()));

    // Preserves the pre-migration default route ("/" showed the dashboard, matching MVC's
    // {controller=Dashboard}/{action=Index} convention route).
    options.Conventions.AddPageRoute("/Dashboard", "");

    options.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/Login");
    options.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/Logout");
    options.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/AccessDenied");
    options.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/Lockout");
    options.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/ForgotPassword");
    options.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/ForgotPasswordConfirmation");
    options.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/ResetPassword");
    options.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/ResetPasswordConfirmation");

    // Explicitly require admin authorization for the Register page —
    // user creation is an admin-only action, not a general sign-up flow.
    options.Conventions.AuthorizeAreaPage("Identity", "/Account/Register", Policies.AdminOnly);
  }
}
