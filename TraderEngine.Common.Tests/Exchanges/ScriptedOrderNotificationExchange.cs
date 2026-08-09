using Microsoft.Extensions.Logging;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Exchanges;
using TraderEngine.Common.Models;
using TraderEngine.Common.Results;

namespace TraderEngine.Common.Tests.Exchanges;

/// <summary>
/// Wraps a <see cref="ScriptedExchange"/> (sealed, so composed rather than subclassed) and
/// additionally implements <see cref="IExchangeOrderNotifications"/>, tracking how many times a
/// session is begun/disposed, for exercising <see cref="Services.RebalancingService.Rebalance"/>'s
/// session-scoping wrapper.
/// </summary>
internal sealed class ScriptedOrderNotificationExchange : IExchange, IExchangeOrderNotifications
{
  private readonly ScriptedExchange _inner;

  public int BeginSessionCallCount { get; private set; }

  public int SessionDisposeCallCount { get; private set; }

  public ScriptedOrderNotificationExchange(ScriptedExchange inner)
  {
    _inner = inner;
  }

  public Task<IAsyncDisposable> BeginOrderNotificationSessionAsync(ExchangeCredentials credentials, CancellationToken ct = default)
  {
    BeginSessionCallCount++;

    return Task.FromResult<IAsyncDisposable>(new TrackingDisposable(() => SessionDisposeCallCount++));
  }

  public Task<OrderDto?> WaitForOrderEndedAsync(ExchangeCredentials credentials, OrderDto order, TimeSpan timeout, CancellationToken ct = default)
  {
    throw new NotImplementedException();
  }

  // ── IExchange: delegate everything to the wrapped ScriptedExchange ──────────

  public ILogger<IExchange>? Logger => _inner.Logger;

  public string QuoteSymbol => _inner.QuoteSymbol;

  public decimal MinOrderSizeInQuote => _inner.MinOrderSizeInQuote;

  public decimal MakerFee => _inner.MakerFee;

  public decimal TakerFee => _inner.TakerFee;

  public Task<Result<Balance, ExchangeErrCodeEnum>> GetBalance(ExchangeCredentials credentials)
  {
    return _inner.GetBalance(credentials);
  }

  public Task<Result<decimal, ExchangeErrCodeEnum>> TotalDeposited(ExchangeCredentials credentials)
  {
    return _inner.TotalDeposited(credentials);
  }

  public Task<Result<decimal, ExchangeErrCodeEnum>> TotalWithdrawn(ExchangeCredentials credentials)
  {
    return _inner.TotalWithdrawn(credentials);
  }

  public Task<MarketDataDto?> GetMarket(ExchangeCredentials credentials, MarketReqDto market)
  {
    return _inner.GetMarket(credentials, market);
  }

  public Task<AssetDataDto?> GetAsset(ExchangeCredentials credentials, string baseSymbol)
  {
    return _inner.GetAsset(credentials, baseSymbol);
  }

  public Task<decimal> GetPrice(ExchangeCredentials credentials, MarketReqDto market)
  {
    return _inner.GetPrice(credentials, market);
  }

  public Task<BestBidAskDto?> GetBestBidAsk(ExchangeCredentials credentials, MarketReqDto market)
  {
    return _inner.GetBestBidAsk(credentials, market);
  }

  public Task<Result<OrderDto, ExchangeErrCodeEnum>> NewOrder(ExchangeCredentials credentials, OrderReqDto order, string source = "API")
  {
    return _inner.NewOrder(credentials, order, source);
  }

  public Task<OrderDto?> GetOrder(ExchangeCredentials credentials, string orderId, MarketReqDto market)
  {
    return _inner.GetOrder(credentials, orderId, market);
  }

  public Task<OrderDto?> CancelOrder(ExchangeCredentials credentials, string orderId, MarketReqDto market, string source = "API")
  {
    return _inner.CancelOrder(credentials, orderId, market, source);
  }

  public Task<IEnumerable<OrderDto>?> GetOpenOrders(ExchangeCredentials credentials, MarketReqDto? market = null)
  {
    return _inner.GetOpenOrders(credentials, market);
  }

  public Task<IEnumerable<OrderDto>?> CancelAllOpenOrders(ExchangeCredentials credentials, MarketReqDto? market = null, string source = "API")
  {
    return _inner.CancelAllOpenOrders(credentials, market, source);
  }

  public Task<Result<IEnumerable<OrderDto>?, ExchangeErrCodeEnum>> SellAllPositions(ExchangeCredentials credentials, string? baseSymbol = null, string source = "API")
  {
    return _inner.SellAllPositions(credentials, baseSymbol, source);
  }

  private sealed class TrackingDisposable : IAsyncDisposable
  {
    private readonly Action _onDispose;

    public TrackingDisposable(Action onDispose)
    {
      _onDispose = onDispose;
    }

    public ValueTask DisposeAsync()
    {
      _onDispose();

      return ValueTask.CompletedTask;
    }
  }
}
