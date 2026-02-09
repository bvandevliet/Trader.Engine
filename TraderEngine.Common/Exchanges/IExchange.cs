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

  public decimal TakerFee { get; }

  public string ApiKey { get; set; }

  public string ApiSecret { get; set; }

  public Task<Result<Balance, ExchangeErrCodeEnum>> GetBalance();

  public Task<Result<decimal, ExchangeErrCodeEnum>> TotalDeposited();

  public Task<Result<decimal, ExchangeErrCodeEnum>> TotalWithdrawn();

  public Task<MarketDataDto?> GetMarket(MarketReqDto market);

  public Task<AssetDataDto?> GetAsset(string baseSymbol);

  public Task<decimal> GetPrice(MarketReqDto market);

  public Task<Result<OrderDto, ExchangeErrCodeEnum>> NewOrder(OrderReqDto order, string source = "API");

  public Task<OrderDto?> GetOrder(string orderId, MarketReqDto market);

  public Task<OrderDto?> CancelOrder(string orderId, MarketReqDto market, string source = "API");

  public Task<IEnumerable<OrderDto>?> GetOpenOrders(MarketReqDto? market = null);

  public Task<IEnumerable<OrderDto>?> CancelAllOpenOrders(MarketReqDto? market = null, string source = "API");

  public Task<Result<IEnumerable<OrderDto>?, ExchangeErrCodeEnum>> SellAllPositions(string? baseSymbol = null, string source = "API");
}