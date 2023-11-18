using AutoMapper;
using System.Net.Http.Json;
using TraderEngine.CLI.Repositories;
using TraderEngine.CLI.Services;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Helpers;
using TraderEngine.Common.Models;
using TraderEngine.Common.Repositories;

namespace TraderEngine.CLI;

internal class WorkerService
{
  private readonly Program.AppArgs _appArgs;
  private readonly ILogger<WorkerService> _logger;
  private readonly IMapper _mapper;
  private readonly HttpClient _httpClient;
  private readonly IMarketCapExternalRepository _marketCapExtRepo;
  private readonly IMarketCapInternalRepository _marketCapIntRepo;
  private readonly IApiCredentialsRepository _keyRepo;
  private readonly IConfigRepository _configRepo;
  private readonly IEmailNotificationService _emailNotification;

  public WorkerService(
    Program.AppArgs appArgs,
    ILogger<WorkerService> logger,
    IMapper mapper,
    HttpClient httpClient,
    IMarketCapExternalRepository marketCapExtRepo,
    IMarketCapInternalRepository marketCapIntRepo,
    IApiCredentialsRepository keyRepo,
    IConfigRepository configRepo,
    IEmailNotificationService emailNotification)
  {
    _appArgs = appArgs;
    _logger = logger;
    _mapper = mapper;
    _httpClient = httpClient;
    _marketCapExtRepo = marketCapExtRepo;
    _marketCapIntRepo = marketCapIntRepo;
    _keyRepo = keyRepo;
    _configRepo = configRepo;
    _emailNotification = emailNotification;
  }

  public async Task RunAsync()
  {
    try
    {
      if (_appArgs.DoUpdateMarketCap || _appArgs.DoAutomatedTriggers)
      {
        var latest = await _marketCapExtRepo.ListLatest("EUR");

        await _marketCapIntRepo.InsertMany(latest);
      }

      if (_appArgs.DoAutomatedTriggers)
      {
        var userConfigs = await _configRepo.GetConfigs();

        var now = DateTime.UtcNow;

        Parallel.ForEach(userConfigs, async userConfig =>
        {
          try
          {
            var configReqDto = userConfig.Value;

            // Only handle automation enabled configs.
            if (!configReqDto.AutomationEnabled)
            {
              return;
            }

            // Check if rebalance interval has elapsed.
            if (configReqDto.LastRebalance is DateTime lastRebalance &&
              Math.Round((now - lastRebalance).TotalHours, MidpointRounding.ToNegativeInfinity) < configReqDto.IntervalHours)
            {
              return;
            }

            // TODO: Make this configurable !!
            string exchangeName = "Bitvavo";

            // Get API credentials.
            var apiCred = await _keyRepo.GetApiCred(userConfig.Key, exchangeName);

            // Run automation.
            var rebalanceDto = await RunAutomation(exchangeName, configReqDto, apiCred);

            // If no orders were placed, return.
            if (rebalanceDto.Orders.Length == 0)
            {
              return;
            }

            // If any of the orders have not ended, return.
            if (rebalanceDto.Orders.Any(order => !order.HasEnded))
            {
              await _emailNotification.SendAutomationFailed(userConfig.Key, now, rebalanceDto);

              return;
            }

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
        });
      }
    }
    catch (Exception exception)
    {
      _logger.LogCritical(exception, "Error while running worker service.");
    }
  }

  // TODO: BREAK UP INTO MULTIPLE SINGLE-PURPOSE METHODS !!
  public async Task<RebalanceDto> RunAutomation(
    string exchangeName, ConfigReqDto configReqDto, ApiCredReqDto apiCred)
  {
    // Request current balance.
    using var curBalanceResp = await _httpClient
      .PostAsJsonAsync($"api/allocations/current/{exchangeName}", apiCred);

    // TODO: ERROR HANDLING ??
    if (!curBalanceResp.IsSuccessStatusCode)
    {
      _logger.LogError("{url} returned {code} {reason} : {response}",
        $"api/allocations/current/{exchangeName}", (int)curBalanceResp.StatusCode,
        curBalanceResp.ReasonPhrase, await curBalanceResp.Content.ReadAsStringAsync());

      throw new Exception("Error while requesting current balance.");
    }

    // Read current balance DTO.
    var curBalanceDto = (await curBalanceResp.Content.ReadFromJsonAsync<BalanceDto>())!;

    // Get current balance object.
    var curBalance = _mapper.Map<Balance>(curBalanceDto);

    // Construct balance request DTO.
    var balanceReqDto = new BalanceReqDto()
    {
      QuoteSymbol = curBalance.QuoteSymbol,
      AmountQuoteTotal = curBalance.AmountQuoteTotal,
      Config = configReqDto,
      ExchangeApiCred = apiCred,
    };

    // Request absolute balanced allocations.
    using var absAllocsResp = await _httpClient
      .PostAsJsonAsync($"api/allocations/balanced/{exchangeName}", balanceReqDto);

    // TODO: ERROR HANDLING ??
    if (!absAllocsResp.IsSuccessStatusCode)
    {
      _logger.LogError("{url} returned {code} {reason} : {response}",
        $"api/allocations/balanced/{exchangeName}", (int)absAllocsResp.StatusCode,
        absAllocsResp.ReasonPhrase, await absAllocsResp.Content.ReadAsStringAsync());

      throw new Exception("Error while requesting balanced allocations.");
    }

    // Read absolute balanced allocations DTO.
    var absAllocs = (await absAllocsResp.Content.ReadFromJsonAsync<List<AbsAllocReqDto>>())!;

    // Get relative allocation diffs, as list since we're iterating it more than once.
    var allocDiffs = RebalanceHelpers
      .GetAllocationQuoteDiffs(absAllocs, curBalance)
      .ToList();

    // Test if eligible.
    if (!allocDiffs.Any(allocDiff =>
      // .. if any of the allocation diffs exceed the minimum order size.
      (Math.Abs(allocDiff.AmountQuoteDiff) >= configReqDto.MinimumDiffQuote &&
      Math.Abs(allocDiff.AmountQuoteDiff) / curBalance.AmountQuoteTotal >= (decimal)configReqDto.MinimumDiffAllocation / 100)
      // .. or if the asset should not be allocated at all.
      || (allocDiff.Price > 0 && allocDiff.AmountQuoteDiff / allocDiff.Price == allocDiff.Amount)))
    {
      // Return empty rebalance DTO.
      return new RebalanceDto()
      {
        Orders = Array.Empty<OrderDto>(),
        NewBalance = curBalanceDto,
      };
    }

    // Construct rebalance request DTO.
    var rebalanceReqDto = new RebalanceReqDto()
    {
      ExchangeApiCred = apiCred,
      NewAbsAllocs = absAllocs,
      AllocDiffs = allocDiffs,
    };

    // Execute rebalance.
    using var rebalanceResp = await _httpClient
      .PostAsJsonAsync($"api/rebalance/execute/{exchangeName}", rebalanceReqDto);

    // TODO: ERROR HANDLING ??
    if (!rebalanceResp.IsSuccessStatusCode)
    {
      _logger.LogError("{url} returned {code} {reason} : {response}",
        $"api/rebalance/execute/{exchangeName}", (int)rebalanceResp.StatusCode,
        rebalanceResp.ReasonPhrase, await rebalanceResp.Content.ReadAsStringAsync());

      throw new Exception("Error while requesting rebalance execution.");
    }

    // Return resulting rebalance DTO.
    return (await rebalanceResp.Content.ReadFromJsonAsync<RebalanceDto>())!;
  }
}