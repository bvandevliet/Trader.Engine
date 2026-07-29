namespace TraderEngine.Web.Extensions;

public static class SecurityHeadersExtensions
{
  /// <summary>
  /// Security headers — applied in all environments. This app can trigger real exchange trades,
  /// so it gets the same defense-in-depth headers as SimplePlanner.Net's reference pattern.
  /// </summary>
  public static IApplicationBuilder UseTraderEngineSecurityHeaders(this IApplicationBuilder app, IWebHostEnvironment environment)
  {
    return app.Use(async (context, next) =>
    {
      context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
      context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
      context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
      context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
      context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=(), payment=()");
      var csp = environment.IsDevelopment()
        ? "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self' data:; connect-src 'self' ws://localhost:* wss://localhost:* http://localhost:*; frame-ancestors 'self'; form-action 'self';"
        : "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self' data:; frame-ancestors 'self'; form-action 'self';";
      context.Response.Headers.Append("Content-Security-Policy", csp);
      await next();
    });
  }
}
