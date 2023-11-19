using Microsoft.Extensions.Options;
using MySqlConnector;
using TraderEngine.CLI.AppSettings;
using TraderEngine.CLI.Repositories;
using TraderEngine.CLI.Services;
using TraderEngine.Common.Extensions;
using TraderEngine.Common.Factories;
using TraderEngine.Common.Repositories;
using TraderEngine.Common.Services;

namespace TraderEngine.CLI;

public class Program
{
  static void Main(string[] args)
  {
    IHost host = Host.CreateDefaultBuilder(args)
      .ConfigureAppConfiguration(config =>
      {
        config.AddJsonFile("appsettings.Private.json", optional: true, reloadOnChange: true);
      })
      .ConfigureServices((builder, services) =>
      {
        services.AddSingleton(x => new AppArgs(args));

        services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

        services.AddSingleton<INamedTypeFactory<MySqlConnection>, SqlConnectionFactory>();

        services.Configure<AddressSettings>(builder.Configuration.GetSection("Addresses"));

        services.Configure<CmsDbSettings>(builder.Configuration.GetSection("CmsDbSettings"));

        services.Configure<CoinMarketCapSettings>(builder.Configuration.GetSection("CoinMarketCap"));

        services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

        services.AddSingleton<IMarketCapInternalRepository, MarketCapInternalRepository>();

        services.AddHttpClient<IMarketCapExternalRepository, MarketCapExternalRepository>((x, httpClient) =>
        {
          var cmcSettings = x.GetRequiredService<IOptions<CoinMarketCapSettings>>().Value;

          httpClient.BaseAddress = new("https://pro-api.coinmarketcap.com/v1/");

          httpClient.DefaultRequestHeaders.Accept.Add(new("application/json"));

          httpClient.DefaultRequestHeaders.Add("X-CMC_PRO_API_KEY", cmcSettings.API_KEY);
        })
          .ApplyDefaultPoolAndPolicyConfig();

        services.AddHttpClient<ICryptographyService, CryptographyService>((x, httpClient) =>
        {
          var addressSettings = x.GetRequiredService<IOptions<AddressSettings>>().Value;

          httpClient.BaseAddress = new($"{addressSettings.TRADER_CRYPTO}/");

          //httpClient.DefaultRequestHeaders.Add(HeaderNames.Accept, "text/plain");
        })
          .ApplyDefaultPoolAndPolicyConfig();

        services.AddSingleton<IConfigRepository, WordPressConfigRepository>();

        services.AddSingleton<IApiCredentialsRepository, WordPressApiCredRepository>();

        services.AddSingleton<IEmailNotificationService, EmailNotificationService>();

        // Hosted service with HttpClient for API.
        services.AddHttpClient<WorkerService>((x, httpClient) =>
        {
          var addressSettings = x.GetRequiredService<IOptions<AddressSettings>>().Value;

          httpClient.BaseAddress = new($"{addressSettings.TRADER_API}/");

          httpClient.DefaultRequestHeaders.Accept.Add(new("application/json"));
        })
          .ApplyDefaultPoolAndPolicyConfig();
      })
      .Build();

    // App logger.
    ILogger<Program> logger = host.Services.GetService<ILogger<Program>>()!;

#if DEBUG
    // Append app argument when debugging.
    args = args.Append("-marketcap").ToArray();
#endif

    // Run the hosted service, catch-all to guarantee shutdown.
    try
    {
      logger.LogInformation("{time}: Application is starting up ..", DateTime.Now.ToString("u"));

      host.Services.GetRequiredService<WorkerService>().RunAsync().GetAwaiter().GetResult();
    }
    catch (Exception ex)
    {
      logger.LogError(ex, ex.Message);
    }
    finally
    {
      logger.LogInformation("{time}: Application is shutting down ..", DateTime.Now.ToString("u"));

      host.Dispose();
    }
  }

  public class AppArgs
  {
    public AppArgs(string[] args)
    {
      Args = args;

      DoUpdateMarketCap = Args.Contains("-marketcap");
      DoAutomatedTriggers = Args.Contains("-automations");
    }

    public string[] Args { get; }

    public bool DoUpdateMarketCap { get; }
    public bool DoAutomatedTriggers { get; }
  }
}