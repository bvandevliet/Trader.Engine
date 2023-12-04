using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;

namespace TraderEngine.CLI.Services;

public interface IApiClient
{
  public Task<decimal> TotalDeposited(string exchangeName, ApiCredReqDto apiCred);

  public Task<decimal> TotalWithdrawn(string exchangeName, ApiCredReqDto apiCred);

  Task<BalanceDto> CurrentBalance(string exchangeName, ApiCredReqDto apiCred);

  Task<List<AbsAllocReqDto>?> BalancedAbsAllocs(string exchangeName, BalanceReqDto balanceReqDto);

  Task<RebalanceDto> ExecuteRebalance(string exchangeName, RebalanceReqDto rebalanceReqDto);
}