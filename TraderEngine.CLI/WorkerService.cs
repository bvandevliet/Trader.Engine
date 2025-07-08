using MySqlConnector;
using TraderEngine.CLI.Repositories;
using TraderEngine.CLI.Services;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Repositories;

namespace TraderEngine.CLI;

public class WorkerService
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

      int rowsCleared = await _marketCapIntRepo.CleanupDatabase();

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

            if (simulatedResult.ErrorCode != ExchangeErrCodeEnum.Ok)
            {
              _logger.LogError(
                "Error while simulating rebalance for user '{userId}'.", userConfig.Key);

              // Send simulation failure notification.
              await _emailNotification.SendAutomationFailed(
                userConfig.Key, now, "Error while simulating rebalance.",
                simulatedResult.Value?.Orders, simulatedResult.Summary);

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

            // Check if the portfolio is eligible for rebalancing.
            if (!IsEligibleForRebalance(configReqDto, simulated))
            {
              _logger.LogInformation(
                "Portfolio of user '{userId}' was not eligible for rebalancing.", userConfig.Key);

              return;
            }

            // Bail if about to fully sell a non-contiguous larger allocation, starting from the smallest.
            if (HasNonContiguousFullSellOrder(configReqDto, simulated))
            {
              _logger.LogWarning(
                "Skipping automation for user '{userId}' because attempted to fully sell a larger non-contiguous allocation. " +
                "This is just a precaution, if intended, it should be done manually.", userConfig.Key);

              // Send simulation failure notification.
              await _emailNotification.SendAutomationFailed(
                userConfig.Key, now, "Attempted to fully sell a larger non-contiguous allocation. This is just a precaution, if intended, it should be done manually.",
                simulatedResult.Value?.Orders, simulatedResult.Summary, false);

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

            // If any of the orders have not been filled, return.
            if (ordersExecuted.Any(order => order.Status != OrderStatus.Filled))
            {
              _logger.LogError("Not all orders were filled for user '{userId}'.", userConfig.Key);

              // Send failure notification.
              await _emailNotification.SendAutomationFailed(
                userConfig.Key, now, "Not all orders were filled.",
                ordersExecuted, new
                {
                  simulated,
                  ordersExecuted,
                });

              return;
            }

            // If not the same amount of orders were executed and filled as simulated, return.
            if (ordersExecuted.Length != simulated.Orders.Length)
            {
              _logger.LogError(
                "Not all simulated orders were executed for user '{userId}'.", userConfig.Key);

              // Send failure notification.
              await _emailNotification.SendAutomationFailed(
                userConfig.Key, now, "Not all simulated orders were executed.",
                ordersExecuted, new
                {
                  simulated,
                  ordersExecuted,
                });

              return;
            }

            // It could occur that no buy orders were executed if they all were below the minimum required order amount.
            // In that case, don't update last rebalance timestamp to prevent being less exposed to the market for too long.
            if (!ordersExecuted.Any(order => order.Side == OrderSide.Buy && order.Status == OrderStatus.Filled))
            {
              _logger.LogWarning(
                "No buy orders were executed for user '{userId}', not updating last rebalance timestamp.", userConfig.Key);
            }
            else
              configReqDto.LastRebalance = now;

            _logger.LogInformation("Automation completed for user '{userId}'.", userConfig.Key);

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

      // Send exception notification.
      await _emailNotification.SendWorkerException(DateTime.UtcNow, exception);
    }
    finally
    {
      // Clear all connection pools.
      await MySqlConnection.ClearAllPoolsAsync();
    }
  }

  public static bool IsEligibleForRebalance(ConfigReqDto configReqDto, SimulationDto simulated)
  {
    // Test if any of the allocation diffs exceed the minimum order size.
    // Ignoring quote takeout, because it's considered out of the game.
    decimal quoteTakeout = Math.Max(0, Math.Min(configReqDto.QuoteTakeout, simulated.CurBalance.AmountQuoteTotal));
    decimal relTotal = simulated.CurBalance.AmountQuoteTotal - quoteTakeout;
    decimal quoteDiff = simulated.CurBalance.AmountQuoteAvailable - simulated.NewBalance.AmountQuoteAvailable;

    return !(
      // If no orders were simulated, no need to rebalance.
      simulated.Orders.Length == 0 ||
      // If the total portfolio is too small, we can't rebalance.
      relTotal < configReqDto.MinimumDiffQuote ||
      // If quote diff doesn't exceed the minimum order size,
      Math.Abs(quoteDiff) < configReqDto.MinimumDiffQuote &&
      // and none of the simulated orders exceed the minimum order size and diff,
      false == simulated.Orders.Any(order =>
        order.AmountQuoteFilled >= configReqDto.MinimumDiffQuote &&
        order.AmountQuoteFilled / relTotal >= (decimal)configReqDto.MinimumDiffAllocation / 100));
  }

  public static bool HasNonContiguousFullSellOrder(ConfigReqDto configReqDto, SimulationDto simulated)
  {
    // Correlate allocations with simulated orders.
    var allocOrders = simulated.CurBalance.Allocations
      .GroupJoin(
        simulated.Orders,
        alloc => alloc.Market,
        order => order.Market,
        (alloc, orders) => new { Allocation = alloc, Orders = orders });

    // Bail if about to fully sell a non-contiguous larger allocation, starting from the smallest.
    bool potentialGapFound = false;
    return allocOrders
      .OrderBy(x => x.Allocation.AmountQuote)
      .Any(x =>
      {
        if (
          // We are only interested in allocations that are greater than the minimum quote diff,
          x.Allocation.AmountQuote >= configReqDto.MinimumDiffQuote &&
          // and are not being sold as a whole.
          !x.Orders.Any(order => order.Side == OrderSide.Sell && order.Amount == x.Allocation.Amount))
        {
          potentialGapFound = true;
          return false;
        }

        // If current allocation is being sold as a whole and we found a potential gap earlier, bail out.
        return potentialGapFound;
      });
  }
}