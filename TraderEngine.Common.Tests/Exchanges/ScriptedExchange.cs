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

  private readonly Dictionary<string, MarketStatus> _marketStatuses = [];

  private readonly Dictionary<string, decimal> _minOrderSizesInBase = [];

  private readonly Dictionary<string, BestBidAskDto> _bestBidAsks = [];

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
  /// Configures <see cref="GetMarket"/> to report <paramref name="minOrderSizeInBase"/> as the
  /// per-market minimum base-asset order size for <paramref name="baseSymbol"/>. A symbol with no
  /// configured value defaults to 0 (no base-asset floor beyond <see cref="MinOrderSizeInQuote"/>).
  /// </summary>
  public void SetMinOrderSizeInBase(string baseSymbol, decimal minOrderSizeInBase)
  {
    _minOrderSizesInBase[baseSymbol] = minOrderSizeInBase;
  }

  /// <summary>
  /// Configures <see cref="GetBestBidAsk"/> to report <paramref name="bid"/>/<paramref name="ask"/>
  /// for <paramref name="baseSymbol"/>. A symbol with no configured book causes
  /// <see cref="GetBestBidAsk"/> to return <c>null</c>, simulating unavailable book data.
  /// </summary>
  public void SetBestBidAsk(string baseSymbol, decimal bid, decimal ask)
  {
    _bestBidAsks[baseSymbol] = new BestBidAskDto { Bid = bid, Ask = ask };
  }

  /// <summary>
  /// Base symbols for which <see cref="GetBestBidAsk"/> was called, in call order (including repeats).
  /// </summary>
  public List<string> GetBestBidAskCalls { get; } = [];

  /// <summary>
  /// Enqueues the <see cref="OrderDto"/> to be returned by the next <see cref="GetOrder"/> call.
  /// Once the queue is exhausted, subsequent calls return <c>null</c>.
  /// </summary>
  public void EnqueueGetOrderResponse(OrderDto? order)
  {
    _getOrderResponses.Enqueue(order);
  }

  private readonly Queue<Result<OrderDto, ExchangeErrCodeEnum>> _newOrderResponses = new();

  /// <summary>
  /// The <see cref="OrderReqDto"/> passed to each <see cref="NewOrder"/> call, in call order.
  /// </summary>
  public List<OrderReqDto> NewOrderCalls { get; } = [];

  /// <summary>
  /// Enqueues the result to be returned by the next <see cref="NewOrder"/> call.
  /// Once the queue is exhausted, subsequent calls throw <see cref="InvalidOperationException"/>.
  /// </summary>
  public void EnqueueNewOrderResponse(Result<OrderDto, ExchangeErrCodeEnum> result)
  {
    _newOrderResponses.Enqueue(result);
  }

  public Task<MarketDataDto?> GetMarket(ExchangeCredentials credentials, MarketReqDto market)
  {
    GetMarketCalls.Add(market.BaseSymbol);

    var hasStatus = _marketStatuses.TryGetValue(market.BaseSymbol, out var status);
    var hasMinOrderSizeInBase = _minOrderSizesInBase.TryGetValue(market.BaseSymbol, out var minOrderSizeInBase);

    return Task.FromResult(hasStatus || hasMinOrderSizeInBase
      ? new MarketDataDto { Status = status, MinOrderSizeInBase = minOrderSizeInBase }
      : null);
  }

  public Task<OrderDto?> GetOrder(ExchangeCredentials credentials, string orderId, MarketReqDto market)
  {
    GetOrderCalls.Add(orderId);

    return Task.FromResult(_getOrderResponses.Count > 0 ? _getOrderResponses.Dequeue() : null);
  }

  public Task<OrderDto?> CancelOrder(ExchangeCredentials credentials, string orderId, MarketReqDto market, string source = "API")
  {
    CancelOrderCalls.Add((orderId, market));

    if (ThrowOnCancelOrder)
      throw new InvalidOperationException("Simulated cancel failure.");

    return Task.FromResult(CancelOrderResponse);
  }

  public Task<IEnumerable<OrderDto>?> CancelAllOpenOrders(ExchangeCredentials credentials, MarketReqDto? market = null, string source = "API")
  {
    return Task.FromResult(Enumerable.Empty<OrderDto>())!;
  }

  /// <summary>
  /// Configures the <see cref="Balance"/> returned by <see cref="GetBalance"/>. Defaults to an
  /// empty <see cref="EUR"/> balance with a large quote allocation, so buy-side pro-rata scaling
  /// (which reads <see cref="Balance.AmountQuoteAvailable"/>) never constrains a test unless a
  /// smaller balance is explicitly configured.
  /// </summary>
  public Balance BalanceResponse { get; init; } = DefaultBalance();

  private static Balance DefaultBalance()
  {
    var balance = new Balance("EUR");
    balance.TryAddAllocation(new Allocation(new MarketReqDto("EUR", "EUR"), price: 1, amount: 1_000_000));
    return balance;
  }

  public Task<Result<Balance, ExchangeErrCodeEnum>> GetBalance(ExchangeCredentials credentials)
  {
    return Task.FromResult(Result<Balance, ExchangeErrCodeEnum>.Success(BalanceResponse));
  }

  public Task<Result<decimal, ExchangeErrCodeEnum>> TotalDeposited(ExchangeCredentials credentials)
  {
    throw new NotImplementedException();
  }

  public Task<Result<decimal, ExchangeErrCodeEnum>> TotalWithdrawn(ExchangeCredentials credentials)
  {
    throw new NotImplementedException();
  }

  private readonly Dictionary<string, int> _assetDecimals = [];

  /// <summary>
  /// Configures <see cref="GetAsset"/> to report <paramref name="decimals"/> for <paramref name="baseSymbol"/>.
  /// A symbol with no configured decimals causes <see cref="GetAsset"/> to return <c>null</c>.
  /// </summary>
  public void SetAssetDecimals(string baseSymbol, int decimals)
  {
    _assetDecimals[baseSymbol] = decimals;
  }

  public Task<AssetDataDto?> GetAsset(ExchangeCredentials credentials, string baseSymbol)
  {
    return Task.FromResult(_assetDecimals.TryGetValue(baseSymbol, out var decimals)
      ? new AssetDataDto { BaseSymbol = baseSymbol, Name = baseSymbol, Decimals = decimals }
      : null);
  }

  public Task<decimal> GetPrice(ExchangeCredentials credentials, MarketReqDto market)
  {
    throw new NotImplementedException();
  }

  public Task<BestBidAskDto?> GetBestBidAsk(ExchangeCredentials credentials, MarketReqDto market)
  {
    GetBestBidAskCalls.Add(market.BaseSymbol);

    return Task.FromResult(_bestBidAsks.TryGetValue(market.BaseSymbol, out var bidAsk) ? bidAsk : null);
  }

  public Task<Result<OrderDto, ExchangeErrCodeEnum>> NewOrder(ExchangeCredentials credentials, OrderReqDto order, string source = "API")
  {
    NewOrderCalls.Add(order);

    if (_newOrderResponses.Count == 0)
      throw new InvalidOperationException("No scripted NewOrder response was enqueued.");

    return Task.FromResult(_newOrderResponses.Dequeue());
  }

  public Task<IEnumerable<OrderDto>?> GetOpenOrders(ExchangeCredentials credentials, MarketReqDto? market = null)
  {
    throw new NotImplementedException();
  }

  public Task<Result<IEnumerable<OrderDto>?, ExchangeErrCodeEnum>> SellAllPositions(ExchangeCredentials credentials, string? baseSymbol = null, string source = "API")
  {
    throw new NotImplementedException();
  }
}
