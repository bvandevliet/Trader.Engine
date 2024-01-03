using System.Net;
using System.Net.Http.Json;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;

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

  public async Task<decimal> TotalDeposited(string exchangeName, ApiCredReqDto apiCred)
  {
    _logger.LogDebug("Requesting total deposited for '{exchangeName}' ..", exchangeName);

    // Request total deposited.
    using var totalDepositedResp = await _httpClient
      .PostAsJsonAsync($"api/account/totals/deposited/{exchangeName}", apiCred);

    // TODO: ERROR HANDLING ??
    if (!totalDepositedResp.IsSuccessStatusCode)
    {
      _logger.LogError("{url} returned {code} {reason} : {response}",
        $"api/account/totals/deposited/{exchangeName}", (int)totalDepositedResp.StatusCode,
        totalDepositedResp.ReasonPhrase, await totalDepositedResp.Content.ReadAsStringAsync());

      throw new Exception("Error while requesting total deposited.");
    }

    // Read total deposited.
    return (await totalDepositedResp.Content.ReadFromJsonAsync<decimal>())!;
  }

  public async Task<decimal> TotalWithdrawn(string exchangeName, ApiCredReqDto apiCred)
  {
    _logger.LogDebug("Requesting total withdrawn for '{exchangeName}' ..", exchangeName);

    // Request total withdrawn.
    using var totalWithdrawnResp = await _httpClient
      .PostAsJsonAsync($"api/account/totals/withdrawn/{exchangeName}", apiCred);

    // TODO: ERROR HANDLING ??
    if (!totalWithdrawnResp.IsSuccessStatusCode)
    {
      _logger.LogError("{url} returned {code} {reason} : {response}",
        $"api/account/totals/withdrawn/{exchangeName}", (int)totalWithdrawnResp.StatusCode,
        totalWithdrawnResp.ReasonPhrase, await totalWithdrawnResp.Content.ReadAsStringAsync());

      throw new Exception("Error while requesting total withdrawn.");
    }

    // Read total withdrawn.
    return (await totalWithdrawnResp.Content.ReadFromJsonAsync<decimal>())!;
  }

  public async Task<BalanceDto> CurrentBalance(string exchangeName, ApiCredReqDto apiCred)
  {
    _logger.LogDebug("Requesting current balance for '{exchangeName}' ..", exchangeName);

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
    return (await curBalanceResp.Content.ReadFromJsonAsync<BalanceDto>())!;
  }

  public async Task<List<AbsAllocReqDto>?> BalancedAbsAllocs(string exchangeName, BalancedReqDto balancedReqDto)
  {
    _logger.LogDebug("Requesting balanced absolute allocations for '{exchangeName}' ..", exchangeName);

    // Request absolute balanced allocations.
    using var absAllocsResp = await _httpClient
      .PostAsJsonAsync($"api/allocations/balanced/{exchangeName}", balancedReqDto);

    if (!absAllocsResp.IsSuccessStatusCode)
    {
      if (absAllocsResp.StatusCode == HttpStatusCode.NotFound)
      {
        _logger.LogWarning("No recent market cap records found.");
      }
      else
      {
        _logger.LogError("{url} returned {code} {reason} : {response}",
          $"api/allocations/balanced/{exchangeName}", (int)absAllocsResp.StatusCode,
          absAllocsResp.ReasonPhrase, await absAllocsResp.Content.ReadAsStringAsync());
      }

      return null;
    }

    // Read absolute balanced allocations DTO.
    return (await absAllocsResp.Content.ReadFromJsonAsync<List<AbsAllocReqDto>>())!;
  }

  //public void SimulateRebalance()
  //{
  //}

  public async Task<RebalanceDto> ExecuteRebalance(string exchangeName, RebalanceReqDto rebalanceReqDto)
  {
    _logger.LogDebug("Requesting rebalance execution for '{exchangeName}' ..", exchangeName);

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
