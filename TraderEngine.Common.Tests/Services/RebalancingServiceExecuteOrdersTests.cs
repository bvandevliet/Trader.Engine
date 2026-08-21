using Microsoft.Extensions.Logging.Abstractions;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Exchanges;
using TraderEngine.Common.Models;
using TraderEngine.Common.Services;
using TraderEngine.Common.Tests.Exchanges;

namespace TraderEngine.Common.Tests.Services;

/// <summary>
/// Covers <see cref="RebalancingService.Rebalance(IExchange, IEnumerable{OrderReqDto}, string)"/>,
/// i.e. the overload that just executes a caller-supplied list of orders (no diff computation),
/// used by the "execute orders" API path once a simulated rebalance has been approved.
/// </summary>
[TestClass]
public class RebalancingServiceExecuteOrdersTests
{
  private static readonly IRebalancingService _service = new RebalancingService(NullLogger<RebalancingService>.Instance);

  private static readonly ExchangeCredentials _credentials = new("test-key", "test-secret");

  private static readonly MarketReqDto _eur = new("EUR", "EUR");
  private static readonly MarketReqDto _btc = new("EUR", "BTC");
  private static readonly MarketReqDto _eth = new("EUR", "ETH");

  [TestMethod]
  public async Task Rebalance_GivenOrders_ExecutesSellsBeforeBuys_RegardlessOfInputOrder()
  {
    // Arrange
    var curBalance = new Balance("EUR");
    curBalance.TryAddAllocation(new Allocation(_eur, price: 1, amount: 100));
    curBalance.TryAddAllocation(new Allocation(_btc, price: 1, amount: 200));

    var exchange = new CancelTrackingExchange("EUR", 1, 0, 0, curBalance);

    // Buy listed first in the input, sell second — output must still be sell-then-buy.
    var orders = new[]
    {
      new OrderReqDto { Market = _eth, Side = OrderSide.Buy, Type = OrderType.Market, AmountQuote = 50 },
      new OrderReqDto { Market = _btc, Side = OrderSide.Sell, Type = OrderType.Market, AmountQuote = 30 },
    };

    // Act
    var results = await _service.Rebalance(exchange, _credentials, orders, "Test");

    // Assert
    Assert.AreEqual(2, results.Length);
    Assert.AreEqual(OrderSide.Sell, results[0].Side);
    Assert.AreEqual("BTC", results[0].Market.BaseSymbol);
    Assert.AreEqual(OrderSide.Buy, results[1].Side);
    Assert.AreEqual("ETH", results[1].Market.BaseSymbol);

    Assert.AreEqual(1, exchange.CancelAllOpenOrdersCallCount);
  }

  [TestMethod]
  public async Task Rebalance_GivenOrders_FiltersOutQuoteToQuoteOrder()
  {
    // Arrange
    var curBalance = new Balance("EUR");
    curBalance.TryAddAllocation(new Allocation(_eur, price: 1, amount: 100));
    curBalance.TryAddAllocation(new Allocation(_btc, price: 1, amount: 200));

    var exchange = new MockExchange("EUR", 1, 0, 0, curBalance);

    var orders = new[]
    {
      new OrderReqDto { Market = _eur, Side = OrderSide.Sell, Type = OrderType.Market, AmountQuote = 10 },
      new OrderReqDto { Market = _btc, Side = OrderSide.Sell, Type = OrderType.Market, AmountQuote = 30 },
    };

    // Act
    var results = await _service.Rebalance(exchange, _credentials, orders, "Test");

    // Assert
    Assert.AreEqual(1, results.Length);
    Assert.AreEqual("BTC", results[0].Market.BaseSymbol);
  }

  [TestMethod]
  public async Task Rebalance_GivenOrders_FiltersBuyOrderBelowMinimumOrderSize()
  {
    // Arrange
    var curBalance = new Balance("EUR");
    curBalance.TryAddAllocation(new Allocation(_eur, price: 1, amount: 100));

    var exchange = new MockExchange("EUR", 5, 0, 0, curBalance);

    var orders = new[]
    {
      new OrderReqDto { Market = _btc, Side = OrderSide.Buy, Type = OrderType.Market, AmountQuote = 4 },
      new OrderReqDto { Market = _eth, Side = OrderSide.Buy, Type = OrderType.Market, AmountQuote = 20 },
    };

    // Act
    var results = await _service.Rebalance(exchange, _credentials, orders, "Test");

    // Assert
    Assert.AreEqual(1, results.Length);
    Assert.AreEqual("ETH", results[0].Market.BaseSymbol);
  }

  [TestMethod]
  public async Task Rebalance_GivenOrders_SellBelowMinimumQuoteButWithExplicitAmount_IsNotFiltered()
  {
    // Arrange
    // A sell order below MinOrderSizeInQuote in AmountQuote terms is still executed as long as
    // an explicit (base-currency) Amount is given — this is how full-position dust liquidations
    // (Amount-based) survive the minimum-order-size filter.
    var curBalance = new Balance("EUR");
    curBalance.TryAddAllocation(new Allocation(_eur, price: 1, amount: 0));
    curBalance.TryAddAllocation(new Allocation(_btc, price: 1, amount: 3));

    var exchange = new MockExchange("EUR", 5, 0, 0, curBalance);

    var orders = new[]
    {
      new OrderReqDto { Market = _btc, Side = OrderSide.Sell, Type = OrderType.Market, Amount = 3 },
    };

    // Act
    var results = await _service.Rebalance(exchange, _credentials, orders, "Test");

    // Assert
    Assert.AreEqual(1, results.Length);
    Assert.AreEqual(3m, results[0].Amount);
  }

  [TestMethod]
  public async Task Rebalance_GivenOrders_BuyFundedByDustSellProceeds_WaitsForRealDeposit()
  {
    // Arrange
    // Regression test for a real-world bug: an earlier design pre-scaled every buy's claim by a
    // ratio estimated upfront from projected sell proceeds. This overload has no drift/target-
    // weight context, so that estimate was blind to Amount-only dust sells (no AmountQuote to
    // read) — here that blind spot made the estimate look like 0 available, even though the sell
    // was about to fully fund the buy, permanently dropping the buy's claim (0 * ratio = 0) and
    // leaving the sell's real proceeds sitting unspent as leftover EUR. BudgetLedger no longer
    // estimates a ratio at all (see its own remarks): the buy just requests its full target and
    // waits for the dust sell's real, settled deposit to land, which now correctly funds it.
    var curBalance = new Balance("EUR");
    curBalance.TryAddAllocation(new Allocation(_eur, price: 1, amount: 0));
    curBalance.TryAddAllocation(new Allocation(_btc, price: 1, amount: 100));

    var exchange = new MockExchange("EUR", 5, 0, 0, curBalance);

    var orders = new[]
    {
      // Dust/full-liquidation sell: Amount-based, no AmountQuote, invisible to the estimate.
      new OrderReqDto { Market = _btc, Side = OrderSide.Sell, Type = OrderType.Market, Amount = 100 },
      // Just above the exchange minimum — would be dropped by a ratio of 0, but the sell's real
      // 100 EUR proceeds easily cover it once reconciled.
      new OrderReqDto { Market = _eth, Side = OrderSide.Buy, Type = OrderType.Market, AmountQuote = 6 },
    };

    // Act
    var results = await _service.Rebalance(exchange, _credentials, orders, "Test");

    // Assert
    Assert.AreEqual(2, results.Length);

    var eth = results.Single(o => o.Market.BaseSymbol == "ETH");
    Assert.AreEqual(OrderSide.Buy, eth.Side);
    Assert.AreEqual(6m, eth.AmountQuoteFilled);
  }

  [TestMethod]
  public async Task Rebalance_GivenOrders_MarketSellBelowBaseAssetMinimum_IsDropped_NoOrderPlaced()
  {
    // Arrange
    // A market sell with an explicit (base-currency) Amount is checked directly against the
    // market's own base-asset floor, the same as a limit order's finalized Amount already was —
    // market orders previously skipped this check entirely (PlaceAndVerifyOrder only ran it for
    // OrderType.Limit), relying on the exchange to reject the order instead of catching it here.
    var exchange = new ScriptedExchange { MinOrderSizeInQuote = 1 };
    exchange.SetMinOrderSizeInBase("BTC", 0.01m);

    var orders = new[]
    {
      new OrderReqDto { Market = _btc, Side = OrderSide.Sell, Type = OrderType.Market, Amount = 0.005m },
    };

    // Act
    var results = await _service.Rebalance(exchange, _credentials, orders, "Test");

    // Assert
    Assert.AreEqual(0, results.Length);
    Assert.AreEqual(0, exchange.NewOrderCalls.Count);
  }

  [TestMethod]
  public async Task Rebalance_GivenOrders_MarketBuyEstimatedBelowBaseAssetMinimum_IsDropped_NoOrderPlaced()
  {
    // Arrange
    // A market buy sized by AmountQuote has no explicit Amount yet, so the base-asset floor check
    // estimates one from the current best ask, the same estimation basis a limit order already
    // used for its own (exact, since a limit order always has an explicit Amount) check.
    var exchange = new ScriptedExchange { MinOrderSizeInQuote = 1 };
    exchange.SetMinOrderSizeInBase("BTC", 1m);
    exchange.SetBestBidAsk("BTC", bid: 100, ask: 101);

    var orders = new[]
    {
      // 50 / 101 ~= 0.495 BTC, well under the 1 BTC floor.
      new OrderReqDto { Market = _btc, Side = OrderSide.Buy, Type = OrderType.Market, AmountQuote = 50 },
    };

    // Act
    var results = await _service.Rebalance(exchange, _credentials, orders, "Test");

    // Assert
    Assert.AreEqual(0, results.Length);
    Assert.AreEqual(0, exchange.NewOrderCalls.Count);
  }

  /// <summary>
  /// Thin <see cref="MockExchange"/> spy that counts <see cref="CancelAllOpenOrders"/> calls.
  /// Uses the same base-class-hiding pattern as the production <see cref="SimExchange"/>.
  /// </summary>
  private sealed class CancelTrackingExchange : MockExchange, IExchange
  {
    public int CancelAllOpenOrdersCallCount { get; private set; }

    public CancelTrackingExchange(
      string quoteSymbol, decimal minOrderSize, decimal makerFee, decimal takerFee, Balance curBalance)
      : base(quoteSymbol, minOrderSize, makerFee, takerFee, curBalance)
    {
    }

    public new Task<IEnumerable<OrderDto>?> CancelAllOpenOrders(ExchangeCredentials credentials, MarketReqDto? market = null, string source = "Mock")
    {
      CancelAllOpenOrdersCallCount++;

      return base.CancelAllOpenOrders(credentials, market, source);
    }
  }
}
