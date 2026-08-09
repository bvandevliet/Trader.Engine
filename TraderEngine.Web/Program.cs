using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TraderEngine.Common.Extensions;
using TraderEngine.Data;
using TraderEngine.Data.Constants;
using TraderEngine.Data.Entities;
using TraderEngine.Data.Extensions;
using TraderEngine.Web.AppSettings;
using TraderEngine.Web.Extensions;
using TraderEngine.Web.Identity;
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

    builder.Services.AddTraderEngineJwtSettings(builder.Configuration);

    builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.ConfigureDefaultJsonSerializerOptions());

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
      .AddDefaultTokenProviders()
      .AddClaimsPrincipalFactory<UserClaimsPrincipalFactory>();

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
    // reachable by an already-authenticated user, and further restricted below to Admins only —
    // there is no anonymous sign-up convention for this app, matching how AdminSeed provisions
    // the one operator account on TraderEngine.API's side.
    builder.Services.AddAuthorization(options =>
    {
      options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

      options.AddPolicy(Policies.AdminOnly, policy => policy.RequireRole(Roles.Admin));
    });

    builder.Services.AddRateLimiter(options =>
    {
      options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

      options.AddTraderEngineLoginPolicy();
    });

    builder.Services.AddRazorPages(options => options.ConfigureTraderEngineRazorPages())
      .AddJsonOptions(options => options.JsonSerializerOptions.ConfigureDefaultJsonSerializerOptions());

    // Key ring must be the exact same file-system location, application name and (if configured)
    // protecting certificate TraderEngine.API uses — this host decrypts exchange credentials the
    // API encrypted (and vice versa), so the two hosts share one key ring rather than each
    // keeping their own.
    var resolvedKeyRingPath = builder.Configuration.ResolveDataProtectionKeyRingPath(builder.Environment.ContentRootPath);
    builder.Services.AddSharedDataProtection(builder.Configuration, resolvedKeyRingPath);

    builder.Services.AddTraderEngineSharedServices();

    builder.Services.AddHttpClient<ITraderEngineApiClient, TraderEngineApiClient>((sp, httpClient) =>
    {
      var apiSettings = sp.GetRequiredService<IOptions<TraderEngineApiSettings>>().Value;

      httpClient.BaseAddress = new Uri(apiSettings.BaseUrl);

      // A limit-order rebalance can legitimately run past HttpClient's 100-second default: sells
      // and buys each get up to a 60-second fill-then-fallback window (see
      // RebalancingService.VerifyOrderEnded's default `checks`), and those two phases run
      // sequentially within a single api/rebalance request. Hitting the default here doesn't just
      // surface as a spurious "failure" in the dashboard — RebalanceController's action has no
      // CancellationToken, so the API keeps executing (and placing real orders) after the Web
      // client has already given up and shown an error, which is worse than just waiting longer.
      httpClient.Timeout = TimeSpan.FromMinutes(5);
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

    app.UseTraderEngineSecurityHeaders(app.Environment);

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

    app.UseRateLimiter();

    app.UseMiddleware<MustChangePasswordMiddleware>();

    app.MapHealthChecks("/health").AllowAnonymous();
    app.MapStaticAssets().AllowAnonymous();
    app.MapRazorPages().WithStaticAssets();

    app.Run();
  }
}
