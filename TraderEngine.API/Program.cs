using System.Text;
using System.Threading.Channels;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Polly;
using Polly.Contrib.WaitAndRetry;
using TraderEngine.API.AppSettings;
using TraderEngine.API.Exchanges;
using TraderEngine.API.Factories;
using TraderEngine.API.Repositories;
using TraderEngine.API.Services;
using TraderEngine.Common.Exchanges;
using TraderEngine.Common.Extensions;
using TraderEngine.Common.Repositories;
using TraderEngine.Common.Services;
using TraderEngine.Data;
using TraderEngine.Data.Entities;

namespace TraderEngine.API;

public class Program
{
  private static readonly List<Type> _exchanges =
  [
    typeof(BitvavoExchange),
  ];

  public static void Main(string[] args)
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

    // Every endpoint requires authentication by default (interim, API-only JWT auth backed by
    // AppUser — see AuthController); actions must opt out explicitly via [AllowAnonymous] rather
    // than opt in via [Authorize], so a newly added controller can't accidentally end up
    // reachable without a valid token.
    builder.Services.AddControllers(options =>
      options.Filters.Add(new AuthorizeFilter()));

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
      .AddIdentityCore<AppUser>()
      .AddRoles<IdentityRole<Guid>>()
      .AddSignInManager()
      .AddEntityFrameworkStores<TraderEngineDbContext>();

    var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();

    builder.Services
      .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
      .AddJwtBearer(options =>
      {
        options.TokenValidationParameters = new TokenValidationParameters
        {
          ValidateIssuer = true,
          ValidIssuer = jwtSettings.Issuer,
          ValidateAudience = true,
          ValidAudience = jwtSettings.Audience,
          ValidateLifetime = true,
          ValidateIssuerSigningKey = true,
          IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
        };
      });

    builder.Services.AddAuthorization();

    // Keys are persisted to a dedicated volume rather than TraderEngineDbContext's own database
    // deliberately: the key ring must live in a different failure/compromise domain than the
    // ExchangeApiCredential ciphertext it protects, otherwise a single database compromise
    // defeats the encryption entirely. Mirrors the volume the previous cryptography microservice
    // used for the same purpose (see docker-compose.yml).
    builder.Services
      .AddDataProtection()
      .PersistKeysToFileSystem(new DirectoryInfo(
        builder.Configuration["DataProtection:KeyRingPath"] ?? Path.Combine(AppContext.BaseDirectory, "secrets")))
      .SetApplicationName("TraderEngine");

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
      .ApplyDefaultPoolAndPolicyConfig()
      .AddTransientHttpErrorPolicy(policy =>
        policy.WaitAndRetryAsync(Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(1), 4)));

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
      scope.ServiceProvider.GetRequiredService<TraderEngineDbContext>().Database.Migrate();

      SeedAdminUser(scope.ServiceProvider).GetAwaiter().GetResult();
    }

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    app.Run();
  }

  /// <summary>
  /// Idempotently creates the single operator account from <see cref="AdminSeedSettings"/>, if
  /// configured and not already present. No-op (and never overwrites an existing user) otherwise.
  /// </summary>
  private static async Task SeedAdminUser(IServiceProvider services)
  {
    var seedSettings = services.GetRequiredService<IOptions<AdminSeedSettings>>().Value;

    if (string.IsNullOrEmpty(seedSettings.UserName) ||
      string.IsNullOrEmpty(seedSettings.Email) ||
      string.IsNullOrEmpty(seedSettings.Password))
    {
      return;
    }

    var userManager = services.GetRequiredService<UserManager<AppUser>>();

    if (await userManager.FindByNameAsync(seedSettings.UserName) != null)
      return;

    var user = new AppUser
    {
      UserName = seedSettings.UserName,
      Email = seedSettings.Email,
      DisplayName = seedSettings.UserName,
      EmailConfirmed = true,
    };

    var result = await userManager.CreateAsync(user, seedSettings.Password);

    if (!result.Succeeded)
    {
      var logger = services.GetRequiredService<ILogger<Program>>();
      logger.LogCritical("Failed to seed admin user: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
    }
  }
}
