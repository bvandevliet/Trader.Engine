using System.Threading.Channels;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Polly;
using Polly.Contrib.WaitAndRetry;
using TraderEngine.API.AppSettings;
using TraderEngine.API.Exchanges;
using TraderEngine.API.Factories;
using TraderEngine.API.Repositories;
using TraderEngine.API.Services;
using TraderEngine.Common.Exchanges;
using TraderEngine.Common.Extensions;
using TraderEngine.Common.Factories;
using TraderEngine.Common.Repositories;
using TraderEngine.Common.Services;

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
    builder.Services.AddControllers();

    builder.Services.Configure<AddressSettings>(builder.Configuration.GetSection("Addresses"));
    builder.Services.Configure<CmsDbSettings>(builder.Configuration.GetSection("CmsDbSettings"));
    builder.Services.Configure<CoinMarketCapSettings>(builder.Configuration.GetSection("CoinMarketCap"));
    builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

    builder.Services.AddScoped<INamedTypeFactory<MySqlConnection>, SqlConnectionFactory>();

    builder.Services.AddScoped<IMarketCapInternalRepository, MarketCapInternalRepository>();
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

    builder.Services.AddHttpClient<ICryptographyService, CryptographyService>((x, httpClient) =>
    {
      var addressSettings = x.GetRequiredService<IOptions<AddressSettings>>().Value;

      httpClient.BaseAddress = new($"{addressSettings.TRADER_CRYPTO}/");
    })
      .ApplyDefaultPoolAndPolicyConfig();

    builder.Services.AddScoped<IConfigRepository, WordPressConfigRepository>();
    builder.Services.AddScoped<IApiCredentialsRepository, WordPressApiCredRepository>();

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

    app.MapControllers();

    app.Run();
  }
}
