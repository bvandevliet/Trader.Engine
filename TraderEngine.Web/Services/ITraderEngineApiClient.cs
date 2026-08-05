using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Data.Entities;

namespace TraderEngine.Web.Services;

/// <summary>
/// Thin authenticated wrapper around TraderEngine.API's HTTP surface — the business logic for
/// exchange market data, order signing/execution and rebalancing math lives solely in the API;
/// this client never duplicates it. Every call mints a fresh, short-lived JWT for <paramref
/// name="user"/> via <see cref="Data.Services.IJwtTokenService"/> instead of storing a token,
/// since the Identity cookie session already re-authenticates the caller on every request.
/// </summary>
public interface ITraderEngineApiClient
{
  public Task<decimal> GetTotalDeposited(AppUser user, string exchangeName, ApiCredReqDto credentials, CancellationToken ct = default);

  public Task<decimal> GetTotalWithdrawn(AppUser user, string exchangeName, ApiCredReqDto credentials, CancellationToken ct = default);

  public Task<BalanceDto> GetCurrentBalance(AppUser user, string exchangeName, ApiCredReqDto credentials, CancellationToken ct = default);

  public Task<SimulationDto> SimulateRebalance(AppUser user, string exchangeName, string source, SimulationReqDto request, CancellationToken ct = default);

  public Task<OrderDto[]> Rebalance(AppUser user, string exchangeName, string source, RebalanceReqDto request, CancellationToken ct = default);
}
