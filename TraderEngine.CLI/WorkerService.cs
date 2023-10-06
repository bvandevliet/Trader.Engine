using Microsoft.Extensions.Logging;
using TraderEngine.CLI.Repositories;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Repositories;

namespace TraderEngine.CLI;

public class WorkerService
{
  private readonly Program.AppArgs _appArgs;
  private readonly ILogger<WorkerService> _logger;
  private readonly IMarketCapExternalRepository _marketCapExtRepo;
  private readonly IMarketCapInternalRepository _marketCapIntRepo;

  public WorkerService(
    Program.AppArgs appArgs,
    ILogger<WorkerService> logger,
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