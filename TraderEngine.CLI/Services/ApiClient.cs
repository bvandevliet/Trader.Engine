using System.Net;
using System.Net.Http.Json;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Results;

namespace TraderEngine.CLI.Services;

public class ApiClient : IApiClient
{
  private readonly ILogger<ApiClient> _logger;
  private readonly HttpClient _httpClient;

  public ApiClient(
    ILogger<ApiClient> logger,
    HttpClient httpClient)
  {
    _logger = logger;
    _httpClient = httpClient;
  }

  public async Task<Result<decimal, ExchangeErrCodeEnum>> TotalDeposited(string exchangeName, ApiCredReqDto apiCred)
  {
    _logger.LogDebug("Requesting total deposited for '{exchangeName}' ..", exchangeName);

    using var totalDepositedResp = await _httpClient
      .PostAsJsonAsync($"api/account/totals/deposited/{exchangeName}", apiCred);

    if (!totalDepositedResp.IsSuccessStatusCode)
    {
      if (totalDepositedResp.StatusCode == HttpStatusCode.Unauthorized)
      {
        _logger.LogWarning("Invalid API credentials.");

        return Result<decimal, ExchangeErrCodeEnum>.Failure(ExchangeErrCodeEnum.AuthenticationError);
      }

      _logger.LogError("{url} returned {code} {reason} : {response}",
        $"api/account/totals/deposited/{exchangeName}", (int)totalDepositedResp.StatusCode,
        totalDepositedResp.ReasonPhrase, await totalDepositedResp.Content.ReadAsStringAsync());

      // TODO: ERROR HANDLING ??
      throw new Exception("Error while requesting total deposited.");
    }

    decimal totalDeposited = await totalDepositedResp.Content.ReadFromJsonAsync<decimal>();

    return Result<decimal, ExchangeErrCodeEnum>.Success(totalDeposited);
  }

  public async Task<Result<decimal, ExchangeErrCodeEnum>> TotalWithdrawn(string exchangeName, ApiCredReqDto apiCred)
  {
    _logger.LogDebug("Requesting total withdrawn for '{exchangeName}' ..", exchangeName);

    using var totalWithdrawnResp = await _httpClient
      .PostAsJsonAsync($"api/account/totals/withdrawn/{exchangeName}", apiCred);

    if (!totalWithdrawnResp.IsSuccessStatusCode)
    {
      if (totalWithdrawnResp.StatusCode == HttpStatusCode.Unauthorized)
      {
        _logger.LogWarning("Invalid API credentials.");

        return Result<decimal, ExchangeErrCodeEnum>.Failure(ExchangeErrCodeEnum.AuthenticationError);
      }

      _logger.LogError("{url} returned {code} {reason} : {response}",
        $"api/account/totals/withdrawn/{exchangeName}", (int)totalWithdrawnResp.StatusCode,
        totalWithdrawnResp.ReasonPhrase, await totalWithdrawnResp.Content.ReadAsStringAsync());

      // TODO: ERROR HANDLING ??
      throw new Exception("Error while requesting total withdrawn.");
    }

    decimal totalWithdrawn = await totalWithdrawnResp.Content.ReadFromJsonAsync<decimal>();

    return Result<decimal, ExchangeErrCodeEnum>.Success(totalWithdrawn);
  }

  public async Task<Result<BalanceDto, ExchangeErrCodeEnum>> CurrentBalance(string exchangeName, ApiCredReqDto apiCred)
  {
    _logger.LogDebug("Requesting current balance for '{exchangeName}' ..", exchangeName);

    using var curBalanceResp = await _httpClient
      .PostAsJsonAsync($"api/allocations/current/{exchangeName}", apiCred);

    if (!curBalanceResp.IsSuccessStatusCode)
    {
      if (curBalanceResp.StatusCode == HttpStatusCode.Unauthorized)
      {
        _logger.LogWarning("Invalid API credentials.");

        return Result<BalanceDto, ExchangeErrCodeEnum>.Failure(ExchangeErrCodeEnum.AuthenticationError);
      }

      _logger.LogError("{url} returned {code} {reason} : {response}",
        $"api/allocations/current/{exchangeName}", (int)curBalanceResp.StatusCode,
        curBalanceResp.ReasonPhrase, await curBalanceResp.Content.ReadAsStringAsync());

      // TODO: ERROR HANDLING ??
      throw new Exception("Error while requesting current balance.");
    }

    var curBalance = await curBalanceResp.Content.ReadFromJsonAsync<BalanceDto>();

    return Result<BalanceDto, ExchangeErrCodeEnum>.Success(curBalance!);
  }

  public async Task<List<AbsAllocReqDto>?> BalancedAbsAllocs(string quoteSymbol, ConfigReqDto config)
  {
    _logger.LogDebug("Requesting balanced absolute allocations for '{quoteSymbol}' ..", quoteSymbol);

    using var absAllocsResp = await _httpClient
      .PostAsJsonAsync($"api/allocations/balanced/{quoteSymbol}", config);

    if (!absAllocsResp.IsSuccessStatusCode)
    {
      if (absAllocsResp.StatusCode == HttpStatusCode.NotFound)
      {
        _logger.LogWarning("No recent market cap records found.");
      }
      else
      {
        _logger.LogError("{url} returned {code} {reason} : {response}",
          $"api/allocations/balanced/{quoteSymbol}", (int)absAllocsResp.StatusCode,
          absAllocsResp.ReasonPhrase, await absAllocsResp.Content.ReadAsStringAsync());
      }

      return null;
    }

    return (await absAllocsResp.Content.ReadFromJsonAsync<List<AbsAllocReqDto>>())!;
  }

  public async Task<Result<SimulationDto?, ExchangeErrCodeEnum>> SimulateRebalance(string exchangeName, SimulationReqDto simulationReqDto)
  {
    _logger.LogDebug("Requesting rebalance execution for '{exchangeName}' ..", exchangeName);

    using var simulationResp = await _httpClient
      .PostAsJsonAsync($"api/rebalance/simulate/{exchangeName}", simulationReqDto);

    // TODO: ERROR HANDLING ??
    if (!simulationResp.IsSuccessStatusCode)
    {
      if (simulationResp.StatusCode == HttpStatusCode.NotFound)
      {
        _logger.LogWarning("No recent market cap records found.");

        return Result<SimulationDto?, ExchangeErrCodeEnum>.Success(null);
      }
      else if (simulationResp.StatusCode == HttpStatusCode.Unauthorized)
      {
        _logger.LogWarning("Invalid API credentials.");

        return Result<SimulationDto?, ExchangeErrCodeEnum>.Failure(ExchangeErrCodeEnum.AuthenticationError);
      }

      _logger.LogError("{url} returned {code} {reason} : {response}",
        $"api/rebalance/simulate/{exchangeName}", (int)simulationResp.StatusCode,
        simulationResp.ReasonPhrase, await simulationResp.Content.ReadAsStringAsync());

      throw new Exception("Error while requesting rebalance execution.");
    }

    var simulationDto = await simulationResp.Content.ReadFromJsonAsync<SimulationDto>();

    return Result<SimulationDto?, ExchangeErrCodeEnum>.Success(simulationDto);
  }

  public async Task<OrderDto[]> Rebalance(string exchangeName, RebalanceReqDto rebalanceReqDto)
  {
    _logger.LogDebug("Requesting rebalance execution for '{exchangeName}' ..", exchangeName);

    using var rebalanceResp = await _httpClient
      .PostAsJsonAsync($"api/rebalance/{exchangeName}", rebalanceReqDto);

    // TODO: ERROR HANDLING ??
    if (!rebalanceResp.IsSuccessStatusCode)
    {
      _logger.LogError("{url} returned {code} {reason} : {response}",
        $"api/rebalance/execute/{exchangeName}", (int)rebalanceResp.StatusCode,
        rebalanceResp.ReasonPhrase, await rebalanceResp.Content.ReadAsStringAsync());

      throw new Exception("Error while requesting rebalance execution.");
    }

    return (await rebalanceResp.Content.ReadFromJsonAsync<OrderDto[]>())!;
  }

  public async Task<OrderDto[]> ExecuteOrders(string exchangeName, ExecuteOrdersReqDto executeOrdersReqDto)
  {
    _logger.LogDebug("Requesting rebalance execution for '{exchangeName}' ..", exchangeName);

    using var rebalanceResp = await _httpClient
      .PostAsJsonAsync($"api/rebalance/execute/{exchangeName}", executeOrdersReqDto);

    // TODO: ERROR HANDLING ??
    if (!rebalanceResp.IsSuccessStatusCode)
    {
      _logger.LogError("{url} returned {code} {reason} : {response}",
        $"api/rebalance/execute/{exchangeName}", (int)rebalanceResp.StatusCode,
        rebalanceResp.ReasonPhrase, await rebalanceResp.Content.ReadAsStringAsync());

      throw new Exception("Error while requesting rebalance execution.");
    }

    return (await rebalanceResp.Content.ReadFromJsonAsync<OrderDto[]>())!;
  }
}
