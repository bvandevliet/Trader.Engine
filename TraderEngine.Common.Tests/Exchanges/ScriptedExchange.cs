using Microsoft.Extensions.Logging;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Exchanges;
using TraderEngine.Common.Models;
using TraderEngine.Common.Results;

namespace TraderEngine.Common.Tests.Exchanges;

/// <summary>
/// A minimal, fully scriptable <see cref="IExchange"/> test double.
/// Unlike <see cref="MockExchange"/> it does not simulate balance mutation — it exists to give
/// fine-grained, per-call control over market status lookups and order lifecycle polling, which
/// <see cref="MockExchange"/> either hardcodes (always <see cref="MarketStatus.Trading"/>) or
/// leaves unimplemented (<c>GetOrder</c>/<c>CancelOrder</c> throw).
/// Members not exercised by the tests using this double throw <see cref="NotImplementedException"/>.
/// </summary>
internal sealed class ScriptedExchange : IExchange
{
  public ILogger<IExchange>? Logger => null;

  public string QuoteSymbol { get; }

  public decimal MinOrderSizeInQuote { get; init; } = 5;

  public decimal MakerFee { get; init; } = 0;

  public decimal TakerFee { get; init; } = 0;

  public string ApiKey { get; set; } = string.Empty;

  public string ApiSecret { get; set; } = string.Empty;

  private readonly Dictionary<string, MarketStatus> _marketStatuses = [];

  /// <summary>
  /// Base symbols for which <see cref="GetMarket"/> was called, in call order (including repeats).
  /// </summary>
  public List<string> GetMarketCalls { get; } = [];

  private readonly Queue<OrderDto?> _getOrderResponses = new();

  /// <summary>
  /// Order ids for which <see cref="GetOrder"/> was called, in call order.
  /// </summary>
  public List<string> GetOrderCalls { get; } = [];

  /// <summary>
  /// (orderId, market) pairs for which <see cref="CancelOrder"/> was called, in call order.
  /// </summary>
  public List<(string OrderId, MarketReqDto Market)> CancelOrderCalls { get; } = [];

  public bool ThrowOnCancelOrder { get; init; }

  public OrderDto? CancelOrderResponse { get; init; }

  public ScriptedExchange(string quoteSymbol = "EUR")
  {
    QuoteSymbol = quoteSymbol;
  }

  /// <summary>
  /// Configures <see cref="GetMarket"/> to report <paramref name="status"/> for <paramref name="baseSymbol"/>.
  /// A symbol with no configured status causes <see cref="GetMarket"/> to return <c>null</c>,
  /// simulating a market for which no data could be found.
  /// </summary>
  public void SetMarketStatus(string baseSymbol, MarketStatus status)
  {
    _marketStatuses[baseSymbol] = status;
  }

  /// <summary>
  /// Enqueues the <see cref="OrderDto"/> to be returned by the next <see cref="GetOrder"/> call.
  /// Once the queue is exhausted, subsequent calls return <c>null</c>.
  /// </summary>
  public void EnqueueGetOrderResponse(OrderDto? order)
  {
    _getOrderResponses.Enqueue(order);
  }

  public Task<MarketDataDto?> GetMarket(MarketReqDto market)
  {
    GetMarketCalls.Add(market.BaseSymbol);

    return Task.FromResult(_marketStatuses.TryGetValue(market.BaseSymbol, out var status)
      ? new MarketDataDto { Status = status }
      : null);
  }

  public Task<OrderDto?> GetOrder(string orderId, MarketReqDto market)
  {
    GetOrderCalls.Add(orderId);

    return Task.FromResult(_getOrderResponses.Count > 0 ? _getOrderResponses.Dequeue() : null);
  }

  public Task<OrderDto?> CancelOrder(string orderId, MarketReqDto market, string source = "API")
  {
    CancelOrderCalls.Add((orderId, market));

    if (ThrowOnCancelOrder)
      throw new InvalidOperationException("Simulated cancel failure.");

    return Task.FromResult(CancelOrderResponse);
  }

  public Task<IEnumerable<OrderDto>?> CancelAllOpenOrders(MarketReqDto? market = null, string source = "API")
  {
    return Task.FromResult(Enumerable.Empty<OrderDto>())!;
  }

  public Task<Result<Balance, ExchangeErrCodeEnum>> GetBalance()
  {
    throw new NotImplementedException();
  }

  public Task<Result<decimal, ExchangeErrCodeEnum>> TotalDeposited()
  {
    throw new NotImplementedException();
  }

  public Task<Result<decimal, ExchangeErrCodeEnum>> TotalWithdrawn()
  {
    throw new NotImplementedException();
  }

  public Task<AssetDataDto?> GetAsset(string baseSymbol)
  {
    throw new NotImplementedException();
  }

  public Task<decimal> GetPrice(MarketReqDto market)
  {
    throw new NotImplementedException();
  }

  public Task<Result<OrderDto, ExchangeErrCodeEnum>> NewOrder(OrderReqDto order, string source = "API")
  {
    throw new NotImplementedException();
  }

  public Task<IEnumerable<OrderDto>?> GetOpenOrders(MarketReqDto? market = null)
  {
    throw new NotImplementedException();
  }

  public Task<Result<IEnumerable<OrderDto>?, ExchangeErrCodeEnum>> SellAllPositions(string? baseSymbol = null, string source = "API")
  {
    throw new NotImplementedException();
  }
}
