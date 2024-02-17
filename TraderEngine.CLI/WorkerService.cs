using TraderEngine.CLI.Repositories;
using TraderEngine.CLI.Services;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Repositories;

namespace TraderEngine.CLI;

internal class WorkerService
{
  // TODO: Put quote symbol for market cap records in appsettings.
  private readonly string _quoteSymbol = "EUR";

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
      _ = await _marketCapIntRepo.InitDatabase();

      if (_appArgs.DoUpdateMarketCap)
      {
        _logger.LogInformation("Updating market cap data ..");

        var latest = await _marketCapExtRepo.ListLatest(_quoteSymbol);

        _ = await _marketCapIntRepo.TryInsertMany(latest);
      }

      if (_appArgs.DoAutomations)
      {
        _logger.LogInformation("Running automations ..");

        var userConfigs = await _configRepo.GetConfigs();

        await Task.WhenAll(userConfigs.Select(async userConfig =>
        {
          var now = DateTime.UtcNow;

          try
          {
            var configReqDto = userConfig.Value;

            // Only handle automation enabled configs.
            if (!configReqDto.AutomationEnabled)
            {
              _logger.LogInformation(
                "Automation is disabled for user '{userId}'.", userConfig.Key);

              return;
            }

            // Check if rebalance interval has elapsed.
            if (configReqDto.LastRebalance is DateTime lastRebalance &&
              Math.Round((now - lastRebalance).TotalHours, MidpointRounding.AwayFromZero) < configReqDto.IntervalHours)
            {
              _logger.LogInformation(
                "Rebalance interval has not elapsed for user '{userId}'.", userConfig.Key);

              return;
            }

            // TODO: Make this configurable !!
            //       And if single exchange, directly call into "simulate",
            //       otherwise, first call into "balanced" and then into "simulate" for each exchange.
            string exchangeName = "Bitvavo";

            // Get API credentials.
            var apiCred = await _keyRepo.GetApiCred(userConfig.Key, exchangeName);

            // Construct balance request DTO.
            var simulationReqDto = new SimulationReqDto(apiCred, configReqDto);

            // Get current balance and simulated rebalance.
            var simulatedResult = await _apiClient.SimulateRebalance(exchangeName, simulationReqDto);

            // If API credentials are invalid, bail.
            if (simulatedResult.ErrorCode == ExchangeErrCodeEnum.AuthenticationError)
            {
              _logger.LogWarning(
                "Invalid API credentials for user '{userId}'.", userConfig.Key);

              // Send API authentication failure notification.
              await _emailNotification.SendAutomationApiAuthFailed(userConfig.Key, now);

              return;
            }

            var simulated = simulatedResult.Value;

            // If balanced allocations could not be determined, bail for safety.
            if (null == simulated)
            {
              _logger.LogWarning(
                "Balanced allocations could not be determined for user '{userId}'.", userConfig.Key);

              return;
            }

            // Check if any assets are allocated, if not, bail for safety.
            if (simulated.CurBalance.AmountQuoteAvailable == simulated.CurBalance.AmountQuoteTotal)
            {
              _logger.LogWarning(
                "Skipping automation for user '{userId}' because no assets are allocated. " +
                "Initial investments should be made manually.", userConfig.Key);

              return;
            }

            // Check if any assets are trading, if not, bail for safety.
            if (!simulated.NewAbsAllocs.Any(absAlloc => absAlloc.MarketStatus == MarketStatus.Trading))
            {
              _logger.LogWarning(
                "Skipping automation for user '{userId}' because no assets would be traded or allocated. " +
                "This may indicate an error at the API server.", userConfig.Key);

              return;
            }

            // Test if any of the allocation diffs exceed the minimum order size.
            // Ignoring quote takeout, because it's considered out of the game.
            // TODO: Put in separate method to enable for unit test !!
            decimal quoteTakeout = Math.Max(0, Math.Min(configReqDto.QuoteTakeout, simulated.CurBalance.AmountQuoteTotal));
            decimal relTotal = simulated.CurBalance.AmountQuoteTotal - quoteTakeout;
            decimal quoteDiff = simulated.CurBalance.AmountQuoteAvailable - simulated.NewBalance.AmountQuoteAvailable;
            if (
              // If no orders were simulated, no need to rebalance.
              simulated.Orders.Length == 0 ||
              // If the total portfolio is too small, we can't rebalance.
              relTotal < configReqDto.MinimumDiffQuote ||
              // If quote diff and none of the simulated orders exceed the minimum order size, no need to rebalance.
              false == (
                Math.Abs(quoteDiff) >= configReqDto.MinimumDiffQuote &&
                Math.Abs(quoteDiff) / relTotal >= (decimal)configReqDto.MinimumDiffAllocation / 100) &&
              false == simulated.Orders.Any(order =>
                order.AmountQuoteFilled >= configReqDto.MinimumDiffQuote &&
                order.AmountQuoteFilled / relTotal >= (decimal)configReqDto.MinimumDiffAllocation / 100))
            {
              _logger.LogInformation(
                "Portfolio of user '{userId}' was not eligible for rebalancing.", userConfig.Key);

              return;
            }

            // Construct rebalance request DTO.
            var rebalanceReqDto = new ExecuteOrdersReqDto(apiCred, simulated.Orders);

            // Execute and return resulting rebalance DTO.
            var ordersExecuted = await _apiClient.ExecuteOrders(exchangeName, rebalanceReqDto);

            // If no orders were placed, return.
            if (ordersExecuted.Length == 0)
            {
              _logger.LogWarning(
                "No orders were placed for user '{userId}'.", userConfig.Key);

              return;
            }

            // If any of the orders have not ended, return.
            if (ordersExecuted.Any(order => order.Status != OrderStatus.Filled))
            {
              _logger.LogError("Not all orders were filled for user '{userId}'.", userConfig.Key);

              // Send failure notification.
              await _emailNotification.SendAutomationFailed(userConfig.Key, now, ordersExecuted, new
              {
                simulated,
                ordersExecuted,
              });

              return;
            }

            _logger.LogInformation("Automation completed for user '{userId}'.", userConfig.Key);

            // Update last rebalance timestamp.
            configReqDto.LastRebalance = now;

            // Save last rebalance timestamp.
            _ = await _configRepo.SaveConfig(userConfig.Key, configReqDto);

            // Send success notification.
            var totalDepositedTask = _apiClient.TotalDeposited(exchangeName, apiCred);
            var totalWithdrawnTask = _apiClient.TotalWithdrawn(exchangeName, apiCred);
            _ = await Task.WhenAll(totalDepositedTask, totalWithdrawnTask);
            await _emailNotification.SendAutomationSucceeded(
              userConfig.Key, now, totalDepositedTask.Result.Value, totalWithdrawnTask.Result.Value, simulated, ordersExecuted);
          }
          catch (Exception exception)
          {
            _logger.LogCritical(exception, "Error while processing automation for user '{userId}'.", userConfig.Key);

            try
            {
              // Send exception notification.
              await _emailNotification.SendAutomationException(userConfig.Key, now, exception);
            }
            catch (Exception exception2)
            {
              _logger.LogCritical(exception2, "Error while sending automation exception notification.");
            }
          }
        }));
      }
    }
    catch (Exception exception)
    {
      _logger.LogCritical(exception, "Error while running worker service.");

      try
      {
        // Send exception notification.
        await _emailNotification.SendWorkerException(DateTime.UtcNow, exception);
      }
      catch (Exception exception2)
      {
        _logger.LogCritical(exception2, "Error while sending Worker exception notification.");
      }
    }
  }
}