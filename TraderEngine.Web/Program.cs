using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TraderEngine.Data;
using TraderEngine.Data.AppSettings;
using TraderEngine.Data.Entities;
using TraderEngine.Data.Extensions;
using TraderEngine.Data.Repositories;
using TraderEngine.Data.Services;
using TraderEngine.Web.AppSettings;
using TraderEngine.Web.Middleware;
using TraderEngine.Web.Services;

namespace TraderEngine.Web;

public class Program
{
  public static async Task Main(string[] args)
  {
    var builder = WebApplication.CreateBuilder(args);
#if DEBUG
    // Add private appsettings.json file when debugging.
    builder.Configuration.AddJsonFile("appsettings.Private.json", optional: true, reloadOnChange: true);
#endif

    builder.Services.AddRouting(options => options.LowercaseUrls = true);

    builder.Services.ConfigureTraderEngineForwardedHeaders();

    builder.Services.AddHealthChecks();

    // Fails fast if the shared signing key is missing/too short, rather than only surfacing as
    // a hard-to-trace failure the first time a page tries to mint a token to call the API.
    builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
    (builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings()).ValidateSigningKey();

    builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
    builder.Services.Configure<TraderEngineApiSettings>(builder.Configuration.GetSection("TraderEngineApi"));

    // Shares the same TraderEngineDbContext/Postgres instance as TraderEngine.API — Identity,
    // rebalancing configuration and exchange credentials are one database, not one per host.
    builder.Services.AddDbContext<TraderEngineDbContext>(options => options
      .UseNpgsql(builder.Configuration.GetConnectionString("Postgres"))
      .UseSnakeCaseNamingConvention());

    builder.Services
      .AddIdentity<AppUser, IdentityRole<Guid>>(options => options.ConfigureTraderEngineIdentityPolicy())
      .AddDefaultUI()
      .AddEntityFrameworkStores<TraderEngineDbContext>()
      .AddDefaultTokenProviders();

    builder.Services.AddScoped<IEmailSender<AppUser>, IdentityEmailSender>();

    builder.Services.ConfigureApplicationCookie(options =>
    {
      // Cookies
      options.Cookie.HttpOnly = true;
      options.Cookie.SameSite = SameSiteMode.Strict;
      options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;

      // Paths
      options.AccessDeniedPath = "/identity/account/access-denied";
      options.LoginPath = "/identity/account/login";
      options.LogoutPath = "/identity/account/logout";

      // Expiration
      options.ExpireTimeSpan = TimeSpan.FromHours(8);
      options.SlidingExpiration = true;
    });

    // Every page requires authentication by default; self-registration is therefore only
    // reachable by an already-authenticated (admin-seeded) user — there is no anonymous sign-up
    // convention for this single-operator-style app, matching how AdminSeed provisions the one
    // operator account on TraderEngine.API's side.
    builder.Services.AddAuthorization(options =>
      options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

    // Razor Pages validate antiforgery tokens on unsafe verbs automatically — no equivalent of
    // MVC's AutoValidateAntiforgeryTokenAttribute filter registration is needed.
    builder.Services.AddRazorPages(options =>
    {
      // Kebab-cases page routes (e.g. AccessDenied -> access-denied) to match the hyphenated
      // paths configured below in ConfigureApplicationCookie.
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
    });

    builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
    builder.Services.AddScoped<IConfigRepository, EfConfigRepository>();
    builder.Services.AddScoped<IApiCredentialsRepository, EfApiCredentialsRepository>();

    // Key ring must be the exact same file-system location, application name and (if configured)
    // protecting certificate TraderEngine.API uses — this host decrypts exchange credentials the
    // API encrypted (and vice versa), so the two hosts share one key ring rather than each
    // keeping their own.
    var configuredKeyRingPath = builder.Configuration["DataProtection:KeyRingPath"];
    var resolvedKeyRingPath = string.IsNullOrWhiteSpace(configuredKeyRingPath)
      ? Path.Combine(AppContext.BaseDirectory, "secrets")
      : Path.IsPathRooted(configuredKeyRingPath)
        ? configuredKeyRingPath
        : Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", configuredKeyRingPath));

    builder.Services.AddSharedDataProtection(builder.Configuration, resolvedKeyRingPath);

    builder.Services.AddHttpClient<ITraderEngineApiClient, TraderEngineApiClient>((sp, httpClient) =>
    {
      var apiSettings = sp.GetRequiredService<IOptions<TraderEngineApiSettings>>().Value;

      httpClient.BaseAddress = new Uri(apiSettings.BaseUrl);
    });

    builder.Services.AddHttpClient("IpInfo", httpClient =>
      httpClient.BaseAddress = new Uri("https://ipinfo.io/"));

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
      await scope.ServiceProvider.GetRequiredService<TraderEngineDbContext>().Database.MigrateAsync();
    }

    // Must run before anything that inspects scheme/remote IP (secure-cookie policy, HSTS,
    // logging, the CSP branch below).
    app.UseForwardedHeaders();

    // Security headers — applied in all environments. This app can trigger real exchange trades,
    // so it gets the same defense-in-depth headers as SimplePlanner.Net's reference pattern.
    app.Use(async (context, next) =>
    {
      context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
      context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
      context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
      context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
      context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=(), payment=()");
      var csp = app.Environment.IsDevelopment()
        ? "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self' data:; connect-src 'self' ws://localhost:* wss://localhost:* http://localhost:*; frame-ancestors 'self'; form-action 'self';"
        : "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self' data:; frame-ancestors 'self'; form-action 'self';";
      context.Response.Headers.Append("Content-Security-Policy", csp);
      await next();
    });

    if (app.Environment.IsDevelopment())
    {
      app.UseMigrationsEndPoint();
    }
    else
    {
      app.UseExceptionHandler("/Error");
      app.UseHsts();
    }

    app.UseStaticFiles();
    app.UseRouting();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapHealthChecks("/health").AllowAnonymous();
    app.MapStaticAssets().AllowAnonymous();
    app.MapRazorPages().WithStaticAssets();

    app.Run();
  }
}
