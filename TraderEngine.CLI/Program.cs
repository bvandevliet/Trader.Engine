using Microsoft.Extensions.Options;
using MySqlConnector;
using System.Diagnostics;
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
  public static void Main(string[] args)
  {
    var host = Host.CreateDefaultBuilder(args)
#if DEBUG
      // Add private appsettings.json file when debugging.
      .ConfigureAppConfiguration(config =>
      {
        config.AddJsonFile("appsettings.Private.json", optional: true, reloadOnChange: true);
      })
#endif
#if !DEBUG
      .ConfigureLogging(logging =>
      {
        logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
        logging.AddFilter("System.Net.Http.HttpClient.", LogLevel.Warning);
      })
#endif
      .ConfigureServices((builder, services) =>
      {
        services.AddSingleton(x => new AppArgs(args));

        services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

        services.Configure<AddressSettings>(builder.Configuration.GetSection("Addresses"));

        services.Configure<CmsDbSettings>(builder.Configuration.GetSection("CmsDbSettings"));

        services.Configure<CoinMarketCapSettings>(builder.Configuration.GetSection("CoinMarketCap"));

        services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

        services.AddSingleton<INamedTypeFactory<MySqlConnection>, SqlConnectionFactory>();

        services.AddTransient<IMarketCapInternalRepository, MarketCapInternalRepository>();

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

        services.AddTransient<IConfigRepository, WordPressConfigRepository>();

        services.AddTransient<IApiCredentialsRepository, WordPressApiCredRepository>();

        services.AddTransient<IEmailNotificationService, EmailNotificationService>();

        services.AddHttpClient<IApiClient, ApiClient>((x, httpClient) =>
        {
          var addressSettings = x.GetRequiredService<IOptions<AddressSettings>>().Value;

          httpClient.BaseAddress = new($"{addressSettings.TRADER_API}/");

          httpClient.DefaultRequestHeaders.Accept.Add(new("application/json"));
        })
          .ApplyDefaultPoolAndPolicyConfig();

        // Hosted service as singleton.
        services.AddSingleton<WorkerService>();
      })
      .Build();

    // App logger.
    var logger = host.Services.GetService<ILogger<Program>>()!;

#if DEBUG
    // Append app argument when debugging.
    args = args.Append("-marketcap").ToArray();
#endif

    var stopwatch = Stopwatch.StartNew();

    // Run the hosted service, catch-all to guarantee shutdown.
    try
    {
      logger.LogInformation(
        "{time}: Application is starting up ..",
        DateTime.Now.ToString("u"));

      host.Services.GetRequiredService<WorkerService>().RunAsync().GetAwaiter().GetResult();
    }
    catch (Exception ex)
    {
      logger.LogCritical(ex, ex.Message);
    }
    finally
    {
      stopwatch.Stop();

      logger.LogInformation(
        "{time}: Application is shutting down after {secs} seconds ..",
        DateTime.Now.ToString("u"), Math.Ceiling(stopwatch.Elapsed.TotalSeconds));

      host.Dispose();
    }
  }

  public class AppArgs
  {
    public AppArgs(string[] args)
    {
      Args = args;

      DoUpdateMarketCap = Args.Contains("-marketcap");
      DoAutomations = Args.Contains("-automations");
    }

    public string[] Args { get; }

    public bool DoUpdateMarketCap { get; }
    public bool DoAutomations { get; }
  }
}