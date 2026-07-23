using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Exchanges;
using TraderEngine.Common.Models;
using TraderEngine.Common.Results;

namespace TraderEngine.Common.Tests.Exchanges;

/// <summary>
/// A <see cref="MockExchange"/> that otherwise behaves normally (real balance mutation, so a
/// batch of several orders can be exercised together) but lets individual orders be scripted to
/// fail in specific ways, to test that <c>RebalancingService</c> degrades a single failing
/// order gracefully instead of losing the whole batch. Uses the same base-class-hiding pattern
/// as the production <see cref="SimExchange"/>.
/// </summary>
internal sealed class FailureInjectingExchange : MockExchange, IExchange
{
  private readonly Dictionary<string, Func<Task<Result<OrderDto, ExchangeErrCodeEnum>>>> _newOrderOverrides = [];

  public FailureInjectingExchange(
    string quoteSymbol, decimal minOrderSize, decimal makerFee, decimal takerFee, Balance curBalance)
    : base(quoteSymbol, minOrderSize, makerFee, takerFee, curBalance)
  {
  }

  /// <summary>
  /// <see cref="NewOrder"/> for <paramref name="baseSymbol"/> returns a failure carrying no
  /// order payload at all (a stricter case than the real Bitvavo exchange, which always
  /// returns a non-null "failed order").
  /// </summary>
  public void FailNewOrderWithNoPayload(string baseSymbol)
  {
    _newOrderOverrides[baseSymbol] = () =>
      Task.FromResult(Result<OrderDto, ExchangeErrCodeEnum>.Failure(null, ExchangeErrCodeEnum.Other));
  }

  /// <summary>
  /// <see cref="NewOrder"/> for <paramref name="baseSymbol"/> returns a failure carrying a
  /// synthetic "failed order" payload, matching how the real Bitvavo exchange reports failures.
  /// </summary>
  public void FailNewOrderWithFailedOrderPayload(string baseSymbol, MarketReqDto market)
  {
    _newOrderOverrides[baseSymbol] = () =>
      Task.FromResult(Result<OrderDto, ExchangeErrCodeEnum>.Failure(
        new OrderDto { Market = market, Side = OrderSide.Sell, Status = OrderStatus.Failed },
        ExchangeErrCodeEnum.Other));
  }

  /// <summary>
  /// <see cref="NewOrder"/> for <paramref name="baseSymbol"/> throws, simulating a
  /// transport-level failure (e.g. a network error) while placing the order.
  /// </summary>
  public void ThrowOnNewOrder(string baseSymbol)
  {
    _newOrderOverrides[baseSymbol] = () =>
      throw new InvalidOperationException($"Simulated failure placing order for {baseSymbol}.");
  }

  /// <summary>
  /// <see cref="NewOrder"/> for <paramref name="baseSymbol"/> succeeds with an order that is
  /// not yet ended and carries an Id, so <c>VerifyOrderEnded</c> will poll it via
  /// <see cref="GetOrder"/> — which <see cref="MockExchange"/> leaves unimplemented (throws),
  /// simulating a transient failure while polling an order that was placed successfully.
  /// </summary>
  public void SucceedThenThrowOnPoll(string baseSymbol, MarketReqDto market)
  {
    _newOrderOverrides[baseSymbol] = () =>
      Task.FromResult(Result<OrderDto, ExchangeErrCodeEnum>.Success(new OrderDto
      {
        Id = Guid.NewGuid().ToString(),
        Market = market,
        Side = OrderSide.Sell,
        Status = OrderStatus.New,
      }));
  }

  public new Task<Result<OrderDto, ExchangeErrCodeEnum>> NewOrder(OrderReqDto order, string source = "Mock")
  {
    return _newOrderOverrides.TryGetValue(order.Market.BaseSymbol, out var overrideFn)
      ? overrideFn()
      : base.NewOrder(order, source);
  }
}
