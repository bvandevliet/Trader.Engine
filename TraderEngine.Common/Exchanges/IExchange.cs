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

  public Task<Result<Balance, ExchangeErrCodeEnum>> GetBalance(ExchangeCredentials credentials);

  public Task<Result<decimal, ExchangeErrCodeEnum>> TotalDeposited(ExchangeCredentials credentials);

  public Task<Result<decimal, ExchangeErrCodeEnum>> TotalWithdrawn(ExchangeCredentials credentials);

  public Task<MarketDataDto?> GetMarket(ExchangeCredentials credentials, MarketReqDto market);

  public Task<AssetDataDto?> GetAsset(ExchangeCredentials credentials, string baseSymbol);

  public Task<decimal> GetPrice(ExchangeCredentials credentials, MarketReqDto market);

  public Task<Result<OrderDto, ExchangeErrCodeEnum>> NewOrder(ExchangeCredentials credentials, OrderReqDto order, string source = "API");

  public Task<OrderDto?> GetOrder(ExchangeCredentials credentials, string orderId, MarketReqDto market);

  public Task<OrderDto?> CancelOrder(ExchangeCredentials credentials, string orderId, MarketReqDto market, string source = "API");

  public Task<IEnumerable<OrderDto>?> GetOpenOrders(ExchangeCredentials credentials, MarketReqDto? market = null);

  public Task<IEnumerable<OrderDto>?> CancelAllOpenOrders(ExchangeCredentials credentials, MarketReqDto? market = null, string source = "API");

  public Task<Result<IEnumerable<OrderDto>?, ExchangeErrCodeEnum>> SellAllPositions(ExchangeCredentials credentials, string? baseSymbol = null, string source = "API");
}
