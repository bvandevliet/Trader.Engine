using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Models;
using TraderEngine.Common.Results;

namespace TraderEngine.API.Exchanges;

public interface IExchange
{
  ILogger<IExchange>? Logger { get; }

  string QuoteSymbol { get; }

  decimal MinOrderSizeInQuote { get; }

  decimal MakerFee { get; }

  decimal TakerFee { get; }

  internal string ApiKey { get; set; }

  internal string ApiSecret { get; set; }

  Task<Result<Balance, ExchangeErrCodeEnum>> GetBalance();

  Task<Result<decimal, ExchangeErrCodeEnum>> TotalDeposited();

  Task<Result<decimal, ExchangeErrCodeEnum>> TotalWithdrawn();

  Task<MarketDataDto?> GetMarket(MarketReqDto market);

  Task<decimal> GetPrice(MarketReqDto market);

  Task<OrderDto> NewOrder(OrderReqDto order);

  Task<OrderDto?> GetOrder(string orderId, MarketReqDto market);

  Task<OrderDto?> CancelOrder(string orderId, MarketReqDto market);

  Task<IEnumerable<OrderDto>?> GetOpenOrders(MarketReqDto? market = null);

  Task<IEnumerable<OrderDto>?> CancelAllOpenOrders(MarketReqDto? market = null);

  Task<IEnumerable<OrderDto>?> SellAllPositions(string? baseSymbol = null);
}