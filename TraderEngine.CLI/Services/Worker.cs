using Microsoft.Extensions.Logging;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Services;

namespace TraderEngine.CLI.Services;

public class Worker
{
  private readonly Program.AppArgs _appArgs;
  private readonly ILogger<Worker> _logger;
  private readonly IMarketCapExternalRepository _marketCapExtRepo;
  private readonly IMarketCapInternalRepository _marketCapIntRepo;

  public Worker(
    Program.AppArgs appArgs,
    ILogger<Worker> logger,
    IMarketCapExternalRepository marketCapExtRepo,
    IMarketCapInternalRepository marketCapIntRepo)
  {
    _appArgs = appArgs;
    _logger = logger;

    _marketCapExtRepo = marketCapExtRepo ?? throw new ArgumentNullException(nameof(marketCapExtRepo));
    _marketCapIntRepo = marketCapIntRepo ?? throw new ArgumentNullException(nameof(marketCapIntRepo));
  }

  public async Task RunAsync()
  {
    if (_appArgs.DoUpdateMarketCap || _appArgs.DoAutomatedTriggers)
    {
      IEnumerable<MarketCapDataDto> latest = await _marketCapExtRepo.ListLatest("EUR");

      await _marketCapIntRepo.InsertMany(latest);
    }

    if (_appArgs.DoAutomatedTriggers)
    {
      // Run automations ..
    }
  }
}