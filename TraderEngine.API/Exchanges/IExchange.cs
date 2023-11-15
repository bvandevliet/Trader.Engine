using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Models;

namespace TraderEngine.API.Exchanges;

public interface IExchange
{
  string QuoteSymbol { get; }

  decimal MinOrderSizeInQuote { get; }

  decimal MakerFee { get; }

  decimal TakerFee { get; }

  internal string ApiKey { get; set; }

  internal string ApiSecret { get; set; }

  Task<Balance?> GetBalance();

  Task<decimal?> TotalDeposited();

  Task<decimal?> TotalWithdrawn();

  // TODO: ASSIGN TYPE !!
  Task<object?> GetCandles(MarketReqDto market, CandleInterval interval, int limit);

  Task<MarketDataDto?> GetMarket(MarketReqDto market);

  Task<decimal?> GetPrice(MarketReqDto market);

  Task<OrderDto> NewOrder(OrderReqDto order);

  Task<OrderDto?> GetOrder(string orderId, MarketReqDto? market = null);

  Task<OrderDto?> CancelOrder(string orderId, MarketReqDto? market = null);

  Task<IEnumerable<OrderDto>?> GetOpenOrders(MarketReqDto? market = null);

  Task<IEnumerable<OrderDto>?> CancelAllOpenOrders(MarketReqDto? market = null);

  Task<IEnumerable<OrderDto>?> SellAllPositions(string? baseSymbol = null);
}