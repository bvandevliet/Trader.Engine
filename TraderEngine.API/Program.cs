using System.Text;
using System.Threading.Channels;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using TraderEngine.API.AppSettings;
using TraderEngine.API.Data;
using TraderEngine.API.Exchanges;
using TraderEngine.API.Extensions;
using TraderEngine.API.Factories;
using TraderEngine.API.Repositories;
using TraderEngine.API.Services;
using TraderEngine.Common.Exchanges;
using TraderEngine.Common.Extensions;
using TraderEngine.Common.Services;
using TraderEngine.Data;
using TraderEngine.Data.Entities;
using TraderEngine.Data.Extensions;
using TraderEngine.Data.Repositories;

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

    var jwtSettings = builder.Services.AddTraderEngineJwtSettings(builder.Configuration);

    builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.ConfigureDefaultJsonSerializerOptions());

    builder.Services.Configure<CoinMarketCapSettings>(builder.Configuration.GetSection("CoinMarketCap"));
    builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
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

    builder.Services.AddTraderEngineApiRateLimiting();

    // Every endpoint requires authentication by default (interim, API-only JWT auth backed by
    // AppUser — see AuthController); actions must opt out explicitly via [AllowAnonymous] rather
    // than opt in via [Authorize], so a newly added controller can't accidentally end up
    // reachable without a valid token.
    builder.Services.AddControllers(options => options.Filters.Add(new AuthorizeFilter()))
      .AddJsonOptions(options => options.JsonSerializerOptions.ConfigureDefaultJsonSerializerOptions());

    // Ensures unhandled exceptions and error-status responses come back as
    // application/problem+json instead of an empty body — see UseExceptionHandler() below.
    builder.Services.AddProblemDetails();

    // Keys are persisted to a dedicated volume rather than TraderEngineDbContext's own database
    // deliberately: the key ring must live in a different failure/compromise domain than the
    // ExchangeApiCredential ciphertext it protects, otherwise a single database compromise
    // defeats the encryption entirely. Mirrors the volume the previous cryptography microservice
    // used for the same purpose (see docker-compose.yml). Shared identically with
    // TraderEngine.Web via AddSharedDataProtection — both hosts must decrypt values the other
    // encrypted, so the key ring path, application name and optional protecting certificate must
    // all match exactly between the two.
    var resolvedKeyRingPath = builder.Configuration.ResolveDataProtectionKeyRingPath(builder.Environment.ContentRootPath);
    builder.Services.AddSharedDataProtection(builder.Configuration, resolvedKeyRingPath);

    builder.Services.AddTraderEngineSharedServices();

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
    builder.Services.AddSingleton<BitvavoWebSocketConnectionPool>();
    foreach (var exchangeType in _exchanges) { builder.Services.AddScoped(exchangeType); }
    builder.Services.AddScoped(x => new ExchangeFactory(x, _exchanges));

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
      await MigrateWithRetryAsync(scope.ServiceProvider.GetRequiredService<TraderEngineDbContext>());
      await DbInitializer.InitializeAsync(scope.ServiceProvider);
    }

    // Must run before anything that inspects scheme/remote IP (rate limiter, auth, logging).
    app.UseForwardedHeaders();

    // In Development, unhandled exceptions surface via the framework's own developer exception
    // page; in other environments they're converted to an application/problem+json response by
    // the AddProblemDetails() registration above instead of leaking as an empty 500.
    if (!app.Environment.IsDevelopment())
    {
      app.UseExceptionHandler();
      app.UseHsts();
    }

    app.UseRouting();

    app.UseAuthentication();
    app.UseAuthorization();

    app.UseRateLimiter();

    app.MapHealthChecks("/health").AllowAnonymous();
    app.MapControllers();

    app.Run();
  }

  // A second instance racing to migrate the same database (e.g. two debug sessions, or a stale
  // container not yet torn down before a new one starts) can lose a lock-wait on
  // __EFMigrationsHistory and then fail its own INSERT with a duplicate-key error once the other
  // instance commits first — the schema and history end up consistent either way, so retrying
  // (which re-reads history fresh and applies only whatever's still pending) recovers cleanly
  // instead of crashing the whole app on what's actually a transient race, not a real migration
  // failure.
  private static async Task MigrateWithRetryAsync(TraderEngineDbContext dbContext, int maxAttempts = 3)
  {
    for (var attempt = 1; ; attempt++)
    {
      try
      {
        await dbContext.Database.MigrateAsync();
        return;
      }
      catch (PostgresException ex) when (
        ex.SqlState == PostgresErrorCodes.UniqueViolation &&
        ex.TableName == "__EFMigrationsHistory" &&
        attempt < maxAttempts)
      {
        await Task.Delay(TimeSpan.FromSeconds(2 * attempt));
      }
    }
  }
}
