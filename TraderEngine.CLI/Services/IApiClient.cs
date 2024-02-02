using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Results;

namespace TraderEngine.CLI.Services;

public interface IApiClient
{
  public Task<Result<decimal, ExchangeErrCodeEnum>> TotalDeposited(string exchangeName, ApiCredReqDto apiCred);

  public Task<Result<decimal, ExchangeErrCodeEnum>> TotalWithdrawn(string exchangeName, ApiCredReqDto apiCred);

  Task<Result<BalanceDto, ExchangeErrCodeEnum>> CurrentBalance(string exchangeName, ApiCredReqDto apiCred);

  Task<List<AbsAllocReqDto>?> BalancedAbsAllocs(string quoteSymbol, ConfigReqDto config);

  Task<Result<SimulationDto?, ExchangeErrCodeEnum>> SimulateRebalance(string exchangeName, SimulationReqDto simulationReqDto);

  Task<OrderDto[]> Rebalance(string exchangeName, RebalanceReqDto rebalanceReqDto);

  Task<OrderDto[]> ExecuteOrders(string exchangeName, ExecuteOrdersReqDto executeOrdersReqDto);
}