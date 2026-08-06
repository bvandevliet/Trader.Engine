using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Exchanges;
using TraderEngine.Common.Models;

namespace TraderEngine.Common.Services;

public interface IRebalancingService
{
  /// <summary>
  /// Try update unknown market status in <paramref name="absAlloc"/>.
  /// </summary>
  /// <param name="exchange"></param>
  /// <param name="credentials"></param>
  /// <param name="absAlloc"></param>
  /// <returns>Collection of updated <see cref="AbsAllocReqDto"/>s.</returns>
  public Task<AbsAllocReqDto> FetchMarketStatus(IExchange exchange, ExchangeCredentials credentials, AbsAllocReqDto absAlloc);

  /// <summary>
  /// Get the top ranking assets in <paramref name="absAllocs"/> for this exchange.
  /// </summary>
  /// <param name="exchange"></param>
  /// <param name="credentials"></param>
  /// <param name="absAllocs"></param>
  /// <param name="topRankingCount"></param>
  /// <returns>Collection of updated <see cref="AbsAllocReqDto"/>s.</returns>
  public Task<List<AbsAllocReqDto>> GetTopRankingAllocs(IExchange exchange, ExchangeCredentials credentials, IEnumerable<AbsAllocReqDto> absAllocs, int topRankingCount);

  /// <summary>
  /// A task that will complete when verified that the given <paramref name="order"/> has ended.
  /// If the given order is not completed within given amount of <paramref name="checks"/>, it will be cancelled.
  /// Every new check is performed one second after the previous has been resolved.
  /// </summary>
  /// <param name="exchange"></param>
  /// <param name="credentials"></param>
  /// <param name="order"></param>
  /// <param name="cancel"></param>
  /// <param name="checks"></param>
  /// <returns>Completes when verified that the given <paramref name="order"/> has ended.</returns>
  public Task<OrderDto> VerifyOrderEnded(IExchange exchange, ExchangeCredentials credentials, OrderDto order, bool cancel = true, int checks = 60);

  /// <summary>
  /// Asynchronously performs a portfolio rebalance.
  /// Quote allocation and takeout will be handled.
  /// </summary>
  /// <param name="exchange"></param>
  /// <param name="credentials"></param>
  /// <param name="config">
  /// <see cref="ConfigReqDto.UseLimitOrders"/> is honored for both real execution and
  /// preview/dry-run calls (against a <c>MockExchange</c>/<c>SimExchange</c>) alike: the mock
  /// resolves a limit order's price from the already-cached allocation price at zero extra cost
  /// and fills it instantly, so a preview's estimated fees correctly reflect the maker rate
  /// without ever needing a real order book lookup.
  /// </param>
  /// <param name="newAbsAllocs"></param>
  /// <param name="curBalance"></param>
  /// <param name="source"></param>
  public Task<OrderDto[]> Rebalance(
    IExchange exchange,
    ExchangeCredentials credentials,
    ConfigReqDto config,
    IEnumerable<AbsAllocReqDto> newAbsAllocs,
    Balance? curBalance = null,
    string source = "API");

  /// <summary>
  /// Asynchronously performs a portfolio rebalance.
  /// Just executes the given orders, without any checks.
  /// </summary>
  /// <param name="exchange"></param>
  /// <param name="credentials"></param>
  /// <param name="orders"></param>
  /// <param name="source"></param>
  public Task<OrderDto[]> Rebalance(
    IExchange exchange,
    ExchangeCredentials credentials,
    IEnumerable<OrderReqDto> orders,
    string source = "API");
}
