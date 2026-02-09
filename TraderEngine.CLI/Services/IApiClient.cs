using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Results;

namespace TraderEngine.CLI.Services;

public interface IApiClient
{
  public Task<Result<decimal, ExchangeErrCodeEnum>> TotalDeposited(string exchangeName, ApiCredReqDto apiCred);

  public Task<Result<decimal, ExchangeErrCodeEnum>> TotalWithdrawn(string exchangeName, ApiCredReqDto apiCred);

  public Task<Result<BalanceDto, ExchangeErrCodeEnum>> CurrentBalance(string exchangeName, ApiCredReqDto apiCred);

  public Task<List<AbsAllocReqDto>?> BalancedAbsAllocs(string quoteSymbol, ConfigReqDto config);

  public Task<Result<SimulationDto?, ExchangeErrCodeEnum>> SimulateRebalance(string exchangeName, SimulationReqDto simulationReqDto);

  public Task<OrderDto[]> Rebalance(string exchangeName, RebalanceReqDto rebalanceReqDto);

  public Task<OrderDto[]> ExecuteOrders(string exchangeName, ExecuteOrdersReqDto executeOrdersReqDto);
}