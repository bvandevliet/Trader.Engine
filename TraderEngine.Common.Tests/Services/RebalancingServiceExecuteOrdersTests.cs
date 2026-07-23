using Microsoft.Extensions.Logging.Abstractions;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Exchanges;
using TraderEngine.Common.Models;
using TraderEngine.Common.Services;

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
    var results = await _service.Rebalance(exchange, orders, "Test");

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
    var results = await _service.Rebalance(exchange, orders, "Test");

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
    var results = await _service.Rebalance(exchange, orders, "Test");

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
    var results = await _service.Rebalance(exchange, orders, "Test");

    // Assert
    Assert.AreEqual(1, results.Length);
    Assert.AreEqual(3m, results[0].Amount);
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

    public new Task<IEnumerable<OrderDto>?> CancelAllOpenOrders(MarketReqDto? market = null, string source = "Mock")
    {
      CancelAllOpenOrdersCallCount++;

      return base.CancelAllOpenOrders(market, source);
    }
  }
}
