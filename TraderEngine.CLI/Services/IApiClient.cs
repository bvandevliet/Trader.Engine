using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;

namespace TraderEngine.CLI.Services;

internal interface IApiClient
{
  Task<BalanceDto> CurrentBalance(string exchangeName, ApiCredReqDto apiCred);

  Task<List<AbsAllocReqDto>> BalancedAbsAllocs(string exchangeName, BalanceReqDto balanceReqDto);

  Task<RebalanceDto> ExecuteRebalance(string exchangeName, RebalanceReqDto rebalanceReqDto);
}