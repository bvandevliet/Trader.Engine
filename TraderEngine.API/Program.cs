using System.Security.Claims;
using System.Text;
using System.Threading.Channels;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TraderEngine.API.AppSettings;
using TraderEngine.API.Data;
using TraderEngine.API.Exchanges;
using TraderEngine.API.Factories;
using TraderEngine.API.Repositories;
using TraderEngine.API.Services;
using TraderEngine.Common.Exchanges;
using TraderEngine.Common.Extensions;
using TraderEngine.Common.Services;
using TraderEngine.Data;
using TraderEngine.Data.AppSettings;
using TraderEngine.Data.Entities;
using TraderEngine.Data.Extensions;
using TraderEngine.Data.Repositories;
using TraderEngine.Data.Services;

namespace TraderEngine.API;

public class Program
{
  private static readonly List<Type> _exchanges =
  [
    typeof(BitvavoExchange),
  ];

  public static async Task Main(string[] args)
  {
    var builder = WebApplication.CreateBuilder(args);
#if DEBUG
    // Add private appsettings.json file when debugging.
    builder.Configuration.AddJsonFile("appsettings.Private.json", optional: true, reloadOnChange: true);
#endif
#if !DEBUG
    builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
    builder.Logging.AddFilter("System.Net.Http.HttpClient.", LogLevel.Warning);
#endif

    builder.Services.AddRouting(options => options.LowercaseUrls = true);

    builder.Services.ConfigureTraderEngineForwardedHeaders();

    builder.Services.AddHealthChecks();

    // Fails fast if the shared signing key is missing/too short, rather than only surfacing as
    // a hard-to-trace failure the first time a page tries to mint a token to call the API.
    var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
    (builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings()).ValidateSigningKey();

    // Every endpoint requires authentication by default (interim, API-only JWT auth backed by
    // AppUser — see AuthController); actions must opt out explicitly via [AllowAnonymous] rather
    // than opt in via [Authorize], so a newly added controller can't accidentally end up
    // reachable without a valid token.
    builder.Services.AddControllers(options => options.Filters.Add(new AuthorizeFilter()));

    builder.Services.Configure<CoinMarketCapSettings>(builder.Configuration.GetSection("CoinMarketCap"));
    builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
    builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
    builder.Services.Configure<AdminSeedSettings>(builder.Configuration.GetSection("AdminSeed"));

    // A factory (rather than a plain scoped AddDbContext) so repositories that fan out
    // concurrent work (e.g. EfMarketCapInternalRepository.TryInsertMany) can create their own
    // short-lived context instances — a single DbContext instance is not thread-safe. A scoped
    // TraderEngineDbContext is still available for constructor injection everywhere else, since
    // AddDbContextFactory registers both.
    builder.Services.AddDbContextFactory<TraderEngineDbContext>(options => options
      .UseNpgsql(builder.Configuration.GetConnectionString("Postgres"))
      .UseSnakeCaseNamingConvention());

    builder.Services
      .AddIdentityCore<AppUser>(options => options.ConfigureTraderEngineIdentityPolicy())
      .AddRoles<IdentityRole<Guid>>()
      .AddEntityFrameworkStores<TraderEngineDbContext>()
      .AddSignInManager()
      .AddDefaultTokenProviders();

    builder.Services
      .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
      .AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
      {
        ValidateIssuer = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtSettings.Audience,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
      });

    builder.Services.AddAuthorization();

    builder.Services.AddRateLimiter(options =>
    {
      options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

      // Throttles login attempts per client IP — a second layer of brute-force protection
      // alongside Identity's per-account lockout (ConfigureTraderEngineIdentityPolicy), which
      // only kicks in once a specific account has already failed enough times.
      options.AddFixedWindowLimiter("login", limiterOptions =>
      {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
      });

      // Throttles the trade-triggering endpoints (Rebalance/Allocations controllers) per
      // authenticated user, so a leaked/hijacked JWT can't be used to fire off rapid-repeat
      // exchange orders. Keyed on the user id claim rather than IP: this app is reached only via
      // TraderEngine.Web's internal call, so every request already carries a real user identity.
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

    builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

    // Keys are persisted to a dedicated volume rather than TraderEngineDbContext's own database
    // deliberately: the key ring must live in a different failure/compromise domain than the
    // ExchangeApiCredential ciphertext it protects, otherwise a single database compromise
    // defeats the encryption entirely. Mirrors the volume the previous cryptography microservice
    // used for the same purpose (see docker-compose.yml). Shared identically with
    // TraderEngine.Web via AddSharedDataProtection — both hosts must decrypt values the other
    // encrypted, so the key ring path, application name and optional protecting certificate must
    // all match exactly between the two.
    var configuredKeyRingPath = builder.Configuration["DataProtection:KeyRingPath"];
    var resolvedKeyRingPath = string.IsNullOrWhiteSpace(configuredKeyRingPath)
      ? Path.Combine(AppContext.BaseDirectory, "secrets")
      : Path.IsPathRooted(configuredKeyRingPath)
        ? configuredKeyRingPath
        : Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", configuredKeyRingPath));

    builder.Services.AddSharedDataProtection(builder.Configuration, resolvedKeyRingPath);

    builder.Services.AddScoped<IMarketCapInternalRepository, EfMarketCapInternalRepository>();
    builder.Services.AddScoped<IMarketCapService, MarketCapService>();
    builder.Services.AddScoped<IRebalancingService, RebalancingService>();

    builder.Services.AddHttpClient<IMarketCapExternalRepository, MarketCapExternalRepository>((x, httpClient) =>
    {
      var cmcSettings = x.GetRequiredService<IOptions<CoinMarketCapSettings>>().Value;

      httpClient.BaseAddress = new("https://pro-api.coinmarketcap.com/v1/");
      httpClient.DefaultRequestHeaders.Accept.Add(new("application/json"));
      httpClient.DefaultRequestHeaders.Add("X-CMC_PRO_API_KEY", cmcSettings.API_KEY);
    })
      .ApplyDefaultPoolAndPolicyConfig();

    builder.Services.AddScoped<IConfigRepository, EfConfigRepository>();
    builder.Services.AddScoped<IApiCredentialsRepository, EfApiCredentialsRepository>();

    builder.Services.AddScoped<IEmailNotificationService, EmailNotificationService>();

    builder.Services.AddScoped<IAutomationOrchestrator, AutomationOrchestrator>();

    var automationChannel = Channel.CreateBounded<DateTimeOffset>(new BoundedChannelOptions(1)
    {
      FullMode = BoundedChannelFullMode.DropOldest,
      SingleReader = true,
      SingleWriter = true,
    });
    builder.Services.AddSingleton(automationChannel.Writer);
    builder.Services.AddSingleton(automationChannel.Reader);
    builder.Services.AddHostedService<MarketCapIngestionService>();
    builder.Services.AddHostedService<AutomationRebalancingService>();

    builder.Services.AddHttpClient<IExchange>().ApplyDefaultPoolAndPolicyConfig();
    foreach (var exchangeType in _exchanges) { builder.Services.AddScoped(exchangeType); }
    builder.Services.AddScoped(x => new ExchangeFactory(x, _exchanges));

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
      await scope.ServiceProvider.GetRequiredService<TraderEngineDbContext>().Database.MigrateAsync();
      await DbInitializer.InitializeAsync(scope.ServiceProvider);
    }

    // Must run before anything that inspects scheme/remote IP (rate limiter, auth, logging).
    app.UseForwardedHeaders();

    if (!app.Environment.IsDevelopment())
    {
      app.UseHsts();
    }

    app.UseAuthentication();
    app.UseAuthorization();

    app.UseRateLimiter();

    app.MapHealthChecks("/health").AllowAnonymous();
    app.MapControllers();

    app.Run();
  }
}
