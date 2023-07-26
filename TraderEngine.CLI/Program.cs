using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Polly;
using Polly.Contrib.WaitAndRetry;
using TraderEngine.CLI.AppSettings;
using TraderEngine.CLI.Services;
using TraderEngine.Common.AppSettings;
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

        services.Configure<CoinMarketCapSettings>(builder.Configuration.GetSection("CoinMarketCap"));
        services.Configure<MongoSettings>(builder.Configuration.GetSection("MongoDB"));

        services.AddSingleton<IMongoClient>(x =>
          new MongoClient(x.GetService<IOptions<MongoSettings>>()!.Value.ConnectionString));

        services.AddHttpClient<IMarketCapExternalRepository, MarketCapExternalRepository>()
          .AddTransientHttpErrorPolicy(policy =>
            policy.WaitAndRetryAsync(Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(1), 4)));

        services.AddSingleton<IMarketCapInternalRepository, MarketCapInternalRepository>();

        // Hosted service as ordinary Singleton.
        services.AddSingleton<Worker>();
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

      host.Services.GetRequiredService<Worker>().RunAsync().GetAwaiter().GetResult();
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