using Microsoft.Extensions.Logging;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Models;
using TraderEngine.Common.Results;

namespace TraderEngine.Common.Exchanges;

public interface IExchange
{
  public ILogger<IExchange>? Logger { get; }

  public string QuoteSymbol { get; }

  public decimal MinOrderSizeInQuote { get; }

  public decimal MakerFee { get; }

  /// <summary>
  /// Fetches the taker fee currently in effect for <paramref name="market"/>, or the account's
  /// default fee when <paramref name="market"/> is <see langword="null"/>. Not a flat constant:
  /// exchanges commonly place different markets under different fee categories/tiers, so the
  /// account-wide default doesn't necessarily reflect what a specific market actually charges.
  /// </summary>
  public Task<decimal> GetTakerFee(ExchangeCredentials credentials, MarketReqDto? market = null);

  /// <summary>
  /// <inheritdoc cref="GetTakerFee" path="/summary"/> Fetches the maker rate instead of the taker
  /// rate — used purely for fee-analysis logging today (comparing a fill's realized rate against
  /// both references to tell a maker fill from a taker fill), not for any budget reservation.
  /// </summary>
  public Task<decimal> GetMakerFee(ExchangeCredentials credentials, MarketReqDto? market = null);

  public Task<Result<Balance, ExchangeErrCodeEnum>> GetBalance(ExchangeCredentials credentials);

  public Task<Result<decimal, ExchangeErrCodeEnum>> TotalDeposited(ExchangeCredentials credentials);

  public Task<Result<decimal, ExchangeErrCodeEnum>> TotalWithdrawn(ExchangeCredentials credentials);

  public Task<MarketDataDto?> GetMarket(ExchangeCredentials credentials, MarketReqDto market);

  public Task<AssetDataDto?> GetAsset(ExchangeCredentials credentials, string baseSymbol);

  public Task<decimal> GetPrice(ExchangeCredentials credentials, MarketReqDto market);

  /// <summary>
  /// Get the current best bid/ask prices for <paramref name="market"/>, used to price limit orders.
  /// </summary>
  public Task<BestBidAskDto?> GetBestBidAsk(ExchangeCredentials credentials, MarketReqDto market);

  public Task<Result<OrderDto, ExchangeErrCodeEnum>> NewOrder(ExchangeCredentials credentials, OrderReqDto order,
    string source = "API");

  public Task<OrderDto?> GetOrder(ExchangeCredentials credentials, string orderId, MarketReqDto market);

  public Task<OrderDto?> CancelOrder(ExchangeCredentials credentials, string orderId, MarketReqDto market,
    string source = "API");

  public Task<IEnumerable<OrderDto>?> GetOpenOrders(ExchangeCredentials credentials, MarketReqDto? market = null);

  public Task<IEnumerable<OrderDto>?> CancelAllOpenOrders(ExchangeCredentials credentials, MarketReqDto? market = null,
    string source = "API");

  public Task<Result<IEnumerable<OrderDto>?, ExchangeErrCodeEnum>> SellAllPositions(ExchangeCredentials credentials,
    string? baseSymbol = null, string source = "API");
}