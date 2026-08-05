using Microsoft.Extensions.Logging.Abstractions;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Exchanges;
using TraderEngine.Common.Models;
using TraderEngine.Common.Services;
using TraderEngine.Common.Tests.Exchanges;

namespace TraderEngine.Common.Tests.Services;

/// <summary>
/// Covers how <see cref="RebalancingService.Rebalance(Exchanges.IExchange, ConfigReqDto, IEnumerable{AbsAllocReqDto}, Balance?, string)"/>
/// behaves when placing or verifying an individual order fails — a real-money system must not
/// let one bad order silently discard the results of every other order in the same batch, nor
/// crash with an unhandled exception.
/// </summary>
[TestClass]
public class RebalancingServiceFailureHandlingTests
{
  private static readonly IRebalancingService _service = new RebalancingService(NullLogger<RebalancingService>.Instance);

  private static readonly ExchangeCredentials _credentials = new("test-key", "test-secret");

  private static readonly MarketReqDto _eur = new("EUR", "EUR");
  private static readonly MarketReqDto _btc = new("EUR", "BTC");
  private static readonly MarketReqDto _eth = new("EUR", "ETH");

  /// <summary>
  /// EUR starts with a real cash balance (100) independent of BTC's sale proceeds, so that
  /// ETH's buy can still be assessed on its own terms when BTC's sell is the one being made to
  /// fail — a failed/unmutated BTC position legitimately still leaves less cash available than
  /// the happy path would (no sale proceeds), so ETH's buy is expected to be scaled down
  /// accordingly rather than fail outright.
  /// </summary>
  private static (FailureInjectingExchange Exchange, Balance Balance) NewScenario()
  {
    var curBalance = new Balance("EUR");
    curBalance.TryAddAllocation(new Allocation(_eur, price: 1, amount: 100));
    curBalance.TryAddAllocation(new Allocation(_btc, price: 1, amount: 300));
    curBalance.TryAddAllocation(new Allocation(_eth, price: 1, amount: 100));

    var exchange = new FailureInjectingExchange("EUR", minOrderSize: 1, makerFee: 0, takerFee: 0, curBalance);

    return (exchange, curBalance);
  }

  [TestMethod]
  public async Task Rebalance_OrderFailsWithNoPayload_IsReportedAsFailed_SiblingOrdersStillComplete()
  {
    // Arrange
    var (exchange, curBalance) = NewScenario();
    exchange.FailNewOrderWithNoPayload("BTC");

    var targets = new[] { new AbsAllocReqDto(_btc, .5m), new AbsAllocReqDto(_eth, .5m) };

    // Act
    var orders = await _service.Rebalance(exchange, _credentials, new ConfigReqDto(), targets, curBalance);

    // Assert — no exception propagated (implicit: reaching this line at all), the failing BTC
    // sell is reported as Failed rather than silently vanishing, and ETH's buy still completed.
    Assert.AreEqual(2, orders.Length);

    var btc = orders.Single(o => o.Market.BaseSymbol == "BTC");
    Assert.AreEqual(OrderStatus.Failed, btc.Status);
    Assert.AreEqual(0, btc.AmountQuoteFilled);

    var eth = orders.Single(o => o.Market.BaseSymbol == "ETH");
    Assert.AreEqual(OrderSide.Buy, eth.Side);
    Assert.AreEqual(100m, eth.AmountQuoteFilled);
  }

  [TestMethod]
  public async Task Rebalance_OrderFailsWithFailedOrderPayload_IsReportedAsFailed_SiblingOrdersStillComplete()
  {
    // Arrange
    // Mirrors how the real Bitvavo exchange reports order failures today (a non-null "failed
    // order" payload) — locks in that this already-safe case stays safe.
    var (exchange, curBalance) = NewScenario();
    exchange.FailNewOrderWithFailedOrderPayload("BTC", _btc);

    var targets = new[] { new AbsAllocReqDto(_btc, .5m), new AbsAllocReqDto(_eth, .5m) };

    // Act
    var orders = await _service.Rebalance(exchange, _credentials, new ConfigReqDto(), targets, curBalance);

    // Assert
    Assert.AreEqual(2, orders.Length);

    var btc = orders.Single(o => o.Market.BaseSymbol == "BTC");
    Assert.AreEqual(OrderStatus.Failed, btc.Status);

    var eth = orders.Single(o => o.Market.BaseSymbol == "ETH");
    Assert.AreEqual(100m, eth.AmountQuoteFilled);
  }

  [TestMethod]
  public async Task Rebalance_OrderThrowsWhilePlacing_IsReportedAsFailed_SiblingOrdersStillComplete()
  {
    // Arrange
    // Simulates a transport-level failure (e.g. a network error) rather than a well-formed
    // Result failure from the exchange.
    var (exchange, curBalance) = NewScenario();
    exchange.ThrowOnNewOrder("BTC");

    var targets = new[] { new AbsAllocReqDto(_btc, .5m), new AbsAllocReqDto(_eth, .5m) };

    // Act
    var orders = await _service.Rebalance(exchange, _credentials, new ConfigReqDto(), targets, curBalance);

    // Assert
    Assert.AreEqual(2, orders.Length);

    var btc = orders.Single(o => o.Market.BaseSymbol == "BTC");
    Assert.AreEqual(OrderStatus.Failed, btc.Status);

    var eth = orders.Single(o => o.Market.BaseSymbol == "ETH");
    Assert.AreEqual(100m, eth.AmountQuoteFilled);
  }

  [TestMethod]
  public async Task Rebalance_OrderThrowsWhilePolling_ReturnsLastKnownOrderState_SiblingOrdersStillComplete()
  {
    // Arrange
    // The order is placed successfully but never reaches a terminal state within this test —
    // polling it via GetOrder fails transiently. The order was genuinely placed on the
    // exchange, so its last known (non-terminal) state must be returned rather than a
    // synthetic Failed order, and it must not be lost from the batch.
    var (exchange, curBalance) = NewScenario();
    exchange.SucceedThenThrowOnPoll("BTC", _btc);

    var targets = new[] { new AbsAllocReqDto(_btc, .5m), new AbsAllocReqDto(_eth, .5m) };

    // Act
    var orders = await _service.Rebalance(exchange, _credentials, new ConfigReqDto(), targets, curBalance);

    // Assert
    Assert.AreEqual(2, orders.Length);

    var btc = orders.Single(o => o.Market.BaseSymbol == "BTC");
    Assert.AreEqual(OrderStatus.New, btc.Status);
    Assert.IsNotNull(btc.Id);

    var eth = orders.Single(o => o.Market.BaseSymbol == "ETH");
    Assert.AreEqual(100m, eth.AmountQuoteFilled);
  }

  [TestMethod]
  public async Task Rebalance_BuyOrderFails_DoesNotAffectAlreadyCompletedSellOrders()
  {
    // Arrange
    // The failure happens on the buy side, which runs strictly after the sell side — the sell
    // that already succeeded (and already mutated the balance) must still be reported.
    var (exchange, curBalance) = NewScenario();
    exchange.FailNewOrderWithNoPayload("ETH");

    var targets = new[] { new AbsAllocReqDto(_btc, .5m), new AbsAllocReqDto(_eth, .5m) };

    // Act
    var orders = await _service.Rebalance(exchange, _credentials, new ConfigReqDto(), targets, curBalance);

    // Assert
    Assert.AreEqual(2, orders.Length);

    var btc = orders.Single(o => o.Market.BaseSymbol == "BTC");
    Assert.AreEqual(OrderSide.Sell, btc.Side);
    Assert.AreEqual(50m, btc.AmountQuoteFilled);

    var eth = orders.Single(o => o.Market.BaseSymbol == "ETH");
    Assert.AreEqual(OrderStatus.Failed, eth.Status);
  }
}
