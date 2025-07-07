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

  Task<AssetDataDto?> GetAsset(string baseSymbol);

  Task<decimal> GetPrice(MarketReqDto market);

  Task<Result<OrderDto, ExchangeErrCodeEnum>> NewOrder(OrderReqDto order, string source = "API");

  Task<OrderDto?> GetOrder(string orderId, MarketReqDto market);

  Task<OrderDto?> CancelOrder(string orderId, MarketReqDto market, string source = "API");

  Task<IEnumerable<OrderDto>?> GetOpenOrders(MarketReqDto? market = null);

  Task<IEnumerable<OrderDto>?> CancelAllOpenOrders(MarketReqDto? market = null, string source = "API");

  Task<Result<IEnumerable<OrderDto>?, ExchangeErrCodeEnum>> SellAllPositions(string? baseSymbol = null, string source = "API");
}