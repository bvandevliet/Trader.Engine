using TraderEngine.CLI.Repositories;
using TraderEngine.CLI.Services;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Helpers;
using TraderEngine.Common.Repositories;

namespace TraderEngine.CLI;

internal class WorkerService
{
  private readonly Program.AppArgs _appArgs;
  private readonly ILogger<WorkerService> _logger;
  private readonly IMarketCapExternalRepository _marketCapExtRepo;
  private readonly IMarketCapInternalRepository _marketCapIntRepo;
  private readonly IApiCredentialsRepository _keyRepo;
  private readonly IConfigRepository _configRepo;
  private readonly IApiClient _apiClient;
  private readonly IEmailNotificationService _emailNotification;

  public WorkerService(
    Program.AppArgs appArgs,
    ILogger<WorkerService> logger,
    IMarketCapExternalRepository marketCapExtRepo,
    IMarketCapInternalRepository marketCapIntRepo,
    IApiCredentialsRepository keyRepo,
    IConfigRepository configRepo,
    IApiClient apiClient,
    IEmailNotificationService emailNotification)
  {
    _appArgs = appArgs;
    _logger = logger;
    _marketCapExtRepo = marketCapExtRepo;
    _marketCapIntRepo = marketCapIntRepo;
    _keyRepo = keyRepo;
    _configRepo = configRepo;
    _apiClient = apiClient;
    _emailNotification = emailNotification;
  }

  public async Task RunAsync()
  {
    try
    {
      await _marketCapIntRepo.InitDatabase();

      if (_appArgs.DoUpdateMarketCap)
      {
        _logger.LogInformation("Updating market cap data ..");

        var latest = await _marketCapExtRepo.ListLatest("EUR");

        await _marketCapIntRepo.InsertMany(latest);
      }

      if (_appArgs.DoAutomations)
      {
        _logger.LogInformation("Running automations ..");

        var userConfigs = await _configRepo.GetConfigs();

        var now = DateTime.UtcNow;

        await Task.WhenAll(userConfigs.Select(async userConfig =>
        {
          try
          {
            var configReqDto = userConfig.Value;

            // Only handle automation enabled configs.
            if (!configReqDto.AutomationEnabled)
            {
              _logger.LogInformation("Automation is disabled for user '{userId}'.", userConfig.Key);

              return;
            }

            // Check if rebalance interval has elapsed.
            if (configReqDto.LastRebalance is DateTime lastRebalance &&
              Math.Round((now - lastRebalance).TotalHours, MidpointRounding.ToNegativeInfinity) < configReqDto.IntervalHours)
            {
              _logger.LogInformation("Rebalance interval has not elapsed for user '{userId}'.", userConfig.Key);

              return;
            }

            // TODO: Make this configurable !!
            string exchangeName = "Bitvavo";

            // Get API credentials.
            var apiCred = await _keyRepo.GetApiCred(userConfig.Key, exchangeName);

            // Run automation.
            var rebalanceDto = await RunAutomation(exchangeName, configReqDto, apiCred);

            if (null == rebalanceDto)
            {
              _logger.LogInformation("Portfolio of user '{userId}' was not eligible for rebalancing.", userConfig.Key);

              return;
            }

            // If no orders were placed, return.
            if (rebalanceDto.Orders.Length == 0)
            {
              _logger.LogWarning("No orders were placed for user '{userId}'.", userConfig.Key);

              return;
            }

            // If any of the orders have not ended, return.
            if (rebalanceDto.Orders.Any(order => !order.HasEnded))
            {
              _logger.LogError("Not all orders have ended for user '{userId}'.", userConfig.Key);

              await _emailNotification.SendAutomationFailed(userConfig.Key, now, rebalanceDto);

              return;
            }

            _logger.LogInformation("Automation completed for user '{userId}'.", userConfig.Key);

            // Update last rebalance timestamp.
            configReqDto.LastRebalance = now;

            // Save last rebalance timestamp.
            await _configRepo.SaveConfig(userConfig.Key, configReqDto);

            await _emailNotification.SendAutomationSucceeded(userConfig.Key, now, rebalanceDto);
          }
          catch (Exception exception)
          {
            _logger.LogCritical(exception, "Error while processing automation for user '{userId}'.", userConfig.Key);

            await _emailNotification.SendAutomationException(userConfig.Key, now, exception);
          }
        }));
      }
    }
    catch (Exception exception)
    {
      _logger.LogCritical(exception, "Error while running worker service.");
    }
  }

  public async Task<RebalanceDto?> RunAutomation(
    string exchangeName, ConfigReqDto configReqDto, ApiCredReqDto apiCred)
  {
    // Get current balance DTO.
    var curBalanceDto = await _apiClient.CurrentBalance(exchangeName, apiCred);

    // Construct balance request DTO.
    var balanceReqDto = new BalanceReqDto()
    {
      QuoteSymbol = curBalanceDto.QuoteSymbol,
      AmountQuoteTotal = curBalanceDto.AmountQuoteTotal,
      Config = configReqDto,
      ExchangeApiCred = apiCred,
    };

    // Get absolute balanced allocations DTO.
    var absAllocs = await _apiClient.BalancedAbsAllocs(exchangeName, balanceReqDto);

    // Get relative allocation diffs, as list since we're iterating it more than once.
    var allocDiffs = RebalanceHelpers
      .GetAllocationQuoteDiffs(absAllocs, curBalanceDto)
      .ToList();

    // Test if eligible.
    if (!allocDiffs.Any(allocDiff =>
      ( // .. if any of the allocation diffs exceed the minimum order size.
      Math.Abs(allocDiff.AmountQuoteDiff) >= configReqDto.MinimumDiffQuote &&
      Math.Abs(allocDiff.AmountQuoteDiff) / curBalanceDto.AmountQuoteTotal >= (decimal)configReqDto.MinimumDiffAllocation / 100)))
    {
      return null;
    }

    // Construct rebalance request DTO.
    var rebalanceReqDto = new RebalanceReqDto()
    {
      ExchangeApiCred = apiCred,
      NewAbsAllocs = absAllocs,
      AllocDiffs = allocDiffs,
    };

    // Execute and return resulting rebalance DTO.
    return await _apiClient.ExecuteRebalance(exchangeName, rebalanceReqDto);
  }
}