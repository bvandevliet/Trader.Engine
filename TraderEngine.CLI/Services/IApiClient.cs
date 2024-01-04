using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;

namespace TraderEngine.CLI.Services;

public interface IApiClient
{
  public Task<decimal> TotalDeposited(string exchangeName, ApiCredReqDto apiCred);

  public Task<decimal> TotalWithdrawn(string exchangeName, ApiCredReqDto apiCred);

  Task<BalanceDto> CurrentBalance(string exchangeName, ApiCredReqDto apiCred);

  Task<List<AbsAllocReqDto>?> BalancedAbsAllocs(string quoteSymbol, ConfigReqDto config);

  Task<SimulationDto> SimulateRebalance(string exchangeName, SimulationReqDto simulationReqDto);

  Task<OrderDto[]> ExecuteRebalance(string exchangeName, RebalanceReqDto rebalanceReqDto);
}