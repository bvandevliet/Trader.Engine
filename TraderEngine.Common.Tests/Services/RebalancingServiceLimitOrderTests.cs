using Microsoft.Extensions.Logging.Abstractions;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Exchanges;
using TraderEngine.Common.Models;
using TraderEngine.Common.Results;
using TraderEngine.Common.Services;
using TraderEngine.Common.Tests.Exchanges;

namespace TraderEngine.Common.Tests.Services;

/// <summary>
/// Covers <see cref="RebalancingService.Rebalance(IExchange, Models.ExchangeCredentials, IEnumerable{OrderReqDto}, string)"/>
/// when given <see cref="OrderType.Limit"/> orders: pricing at the best bid/ask, falling back to
/// a market order for any unfilled remainder, and marking the superseded limit leg accordingly.
/// The "times out after the full poll budget" mechanism itself is already covered in isolation by
/// <see cref="RebalancingServiceVerifyOrderEndedTests"/> — these tests instead have the exchange
/// report the limit order as already ended (e.g. cancelled) on the very first poll, so they don't
/// need to wait out the real ~60s default poll budget to exercise the fallback wiring.
/// </summary>
[TestClass]
public class RebalancingServiceLimitOrderTests
{
  private static readonly IRebalancingService _service = new RebalancingService(NullLogger<RebalancingService>.Instance);

  private static readonly ExchangeCredentials _credentials = new("test-key", "test-secret");

  private static readonly MarketReqDto _btc = new("EUR", "BTC");

  [TestMethod]
  public async Task Rebalance_LimitSellFillsOutright_ReturnsSingleEntry_NotSuperseded()
  {
    // Arrange
    var exchange = new ScriptedExchange { MinOrderSizeInQuote = 1 };
    exchange.SetBestBidAsk("BTC", bid: 100, ask: 101);

    exchange.EnqueueNewOrderResponse(Result<OrderDto, ExchangeErrCodeEnum>.Success(new OrderDto
    {
      Id = "limit-1",
      Market = _btc,
      Side = OrderSide.Sell,
      Type = OrderType.Limit,
      Status = OrderStatus.Filled,
      Price = 100,
      Amount = 2,
      AmountFilled = 2,
      AmountRemaining = 0,
    }));

    var orders = new[]
    {
      new OrderReqDto { Market = _btc, Side = OrderSide.Sell, Type = OrderType.Limit, Amount = 2 },
    };

    // Act
    var results = await _service.Rebalance(exchange, _credentials, orders, "Test");

    // Assert
    Assert.AreEqual(1, results.Length);
    Assert.AreEqual(OrderType.Limit, results[0].Type);
    Assert.AreEqual(100m, results[0].Price);
    Assert.IsFalse(results[0].IsSuperseded);
    Assert.AreEqual(0, exchange.GetOrderCalls.Count); // Already ended — no polling needed.

    var submitted = exchange.NewOrderCalls.Single();
    Assert.AreEqual(100m, submitted.Price);
    Assert.AreEqual(2m, submitted.Amount);
  }

  [TestMethod]
  public async Task Rebalance_AbsAllocOverload_UseLimitOrdersEnabled_ConstructsLimitOrders()
  {
    // Arrange
    // BTC (300/400 = 75%) is overweight against its 50% target; ETH (100/400 = 25%) is
    // underweight — mirrors RebalancingServiceTests's plain-Market equivalent, but with
    // UseLimitOrders on: both legs should go out as Limit orders, sell priced at the best bid,
    // buy priced at the best ask.
    var eur = new MarketReqDto("EUR", "EUR");
    var eth = new MarketReqDto("EUR", "ETH");

    var curBalance = new Balance("EUR");
    curBalance.TryAddAllocation(new Allocation(eur, price: 1, amount: 0));
    curBalance.TryAddAllocation(new Allocation(_btc, price: 1, amount: 300));
    curBalance.TryAddAllocation(new Allocation(eth, price: 1, amount: 100));

    // BuyUnderagesAndVerify re-fetches the balance via GetBalance (it isn't given curBalance
    // directly) rather than seeing the sell's proceeds — unlike MockExchange, ScriptedExchange
    // doesn't simulate balance mutation, so this must be scripted explicitly as the post-sell
    // state (same BTC/ETH holdings, EUR now funded by the sell) for the buy's pro-rata ratio to
    // come out to 1 instead of being starved to 0 by a stale EUR=0 snapshot.
    var postSellBalance = new Balance("EUR");
    postSellBalance.TryAddAllocation(new Allocation(eur, price: 1, amount: 100));
    postSellBalance.TryAddAllocation(new Allocation(_btc, price: 1, amount: 300));
    postSellBalance.TryAddAllocation(new Allocation(eth, price: 1, amount: 100));

    var exchange = new ScriptedExchange { MinOrderSizeInQuote = 1, BalanceResponse = postSellBalance };
    exchange.SetBestBidAsk("BTC", bid: 100, ask: 101);
    exchange.SetBestBidAsk("ETH", bid: 10, ask: 11);
    exchange.SetMarketStatus("BTC", MarketStatus.Trading);
    exchange.SetMarketStatus("ETH", MarketStatus.Trading);

    // Sells execute before buys — enqueue in that order.
    exchange.EnqueueNewOrderResponse(Result<OrderDto, ExchangeErrCodeEnum>.Success(new OrderDto
    {
      Id = "limit-sell",
      Market = _btc,
      Side = OrderSide.Sell,
      Type = OrderType.Limit,
      Status = OrderStatus.Filled,
      Price = 100,
      Amount = 1,
      AmountFilled = 1,
      AmountRemaining = 0,
    }));
    exchange.EnqueueNewOrderResponse(Result<OrderDto, ExchangeErrCodeEnum>.Success(new OrderDto
    {
      Id = "limit-buy",
      Market = eth,
      Side = OrderSide.Buy,
      Type = OrderType.Limit,
      Status = OrderStatus.Filled,
      Price = 11,
      Amount = 9,
      AmountFilled = 9,
      AmountRemaining = 0,
    }));

    var targets = new[]
    {
      new AbsAllocReqDto(_btc, .5m),
      new AbsAllocReqDto(eth, .5m),
    };

    var config = new ConfigReqDto { UseLimitOrders = true };

    // Act
    var results = await _service.Rebalance(exchange, _credentials, config, targets, curBalance);

    // Assert
    Assert.AreEqual(2, results.Length);

    Assert.AreEqual(2, exchange.NewOrderCalls.Count);
    Assert.AreEqual(OrderType.Limit, exchange.NewOrderCalls[0].Type);
    Assert.AreEqual(100m, exchange.NewOrderCalls[0].Price); // Sell priced at the bid.
    Assert.AreEqual(OrderType.Limit, exchange.NewOrderCalls[1].Type);
    Assert.AreEqual(11m, exchange.NewOrderCalls[1].Price); // Buy priced at the ask.
  }

  [TestMethod]
  public async Task Rebalance_LimitBuyCancelledUnfilled_FallsBackToMarketForRemainder()
  {
    // Arrange
    var exchange = new ScriptedExchange { MinOrderSizeInQuote = 1 };
    exchange.SetBestBidAsk("BTC", bid: 100, ask: 101);

    // Limit order: 1 BTC requested (100 EUR / 101 ask -> 0.99 BTC, but keep it simple with Amount
    // already set), partially filled (0.4) then cancelled — e.g. an exchange-side cancellation.
    exchange.EnqueueNewOrderResponse(Result<OrderDto, ExchangeErrCodeEnum>.Success(new OrderDto
    {
      Id = "limit-1",
      Market = _btc,
      Side = OrderSide.Buy,
      Type = OrderType.Limit,
      Status = OrderStatus.New,
      Amount = 1,
      AmountFilled = 0,
      AmountRemaining = 1,
    }));

    exchange.EnqueueGetOrderResponse(new OrderDto
    {
      Id = "limit-1",
      Market = _btc,
      Side = OrderSide.Buy,
      Type = OrderType.Limit,
      Status = OrderStatus.Canceled,
      Amount = 1,
      AmountFilled = 0.4m,
      AmountRemaining = 0.6m,
    });

    // Market fallback for the remaining 0.6 BTC.
    exchange.EnqueueNewOrderResponse(Result<OrderDto, ExchangeErrCodeEnum>.Success(new OrderDto
    {
      Id = "market-1",
      Market = _btc,
      Side = OrderSide.Buy,
      Type = OrderType.Market,
      Status = OrderStatus.Filled,
      Amount = 0.6m,
      AmountFilled = 0.6m,
      AmountRemaining = 0,
    }));

    var orders = new[]
    {
      new OrderReqDto { Market = _btc, Side = OrderSide.Buy, Type = OrderType.Limit, AmountQuote = 100 },
    };

    // Act
    var results = await _service.Rebalance(exchange, _credentials, orders, "Test");

    // Assert
    Assert.AreEqual(2, results.Length);

    Assert.IsTrue(results[0].IsSuperseded);
    Assert.AreEqual(OrderType.Limit, results[0].Type);
    Assert.AreEqual(OrderStatus.Canceled, results[0].Status);
    Assert.AreEqual(0.4m, results[0].AmountFilled);

    Assert.IsFalse(results[1].IsSuperseded);
    Assert.AreEqual(OrderType.Market, results[1].Type);
    Assert.AreEqual(OrderStatus.Filled, results[1].Status);
    Assert.AreEqual(0.6m, results[1].AmountFilled);

    Assert.AreEqual(2, exchange.NewOrderCalls.Count);
    // Non-dust remainder (0.6 BTC @ 101 ask = 60.6 EUR, above MinOrderSizeInQuote): valued in
    // quote currency at the limit price rather than carried over as a stale-priced base Amount,
    // so the market fallback settles at whatever the price actually is by execution time.
    Assert.AreEqual(0.6m * 101m, exchange.NewOrderCalls[1].AmountQuote);
    Assert.IsNull(exchange.NewOrderCalls[1].Amount);
    Assert.AreEqual(OrderType.Market, exchange.NewOrderCalls[1].Type);
  }

  [TestMethod]
  public async Task Rebalance_LimitSellCancelledUnfilled_DustRemainder_FallsBackToFullAmountSell()
  {
    // Arrange
    // Remainder is 0.001 BTC @ 100 bid = 0.1 EUR — below MinOrderSizeInQuote (1) — so, mirroring
    // the dust-prevention branch in SellOveragesAndVerify, the fallback should liquidate the exact
    // (asset-decimals-rounded) remaining Amount rather than an AmountQuote the exchange would
    // reject as too small.
    var exchange = new ScriptedExchange { MinOrderSizeInQuote = 1 };
    exchange.SetBestBidAsk("BTC", bid: 100, ask: 101);
    exchange.SetAssetDecimals("BTC", 8);

    exchange.EnqueueNewOrderResponse(Result<OrderDto, ExchangeErrCodeEnum>.Success(new OrderDto
    {
      Id = "limit-1",
      Market = _btc,
      Side = OrderSide.Sell,
      Type = OrderType.Limit,
      Status = OrderStatus.New,
      Amount = 1,
      AmountFilled = 0,
      AmountRemaining = 1,
    }));

    exchange.EnqueueGetOrderResponse(new OrderDto
    {
      Id = "limit-1",
      Market = _btc,
      Side = OrderSide.Sell,
      Type = OrderType.Limit,
      Status = OrderStatus.Canceled,
      Amount = 1,
      AmountFilled = 0.999m,
      AmountRemaining = 0.001m,
    });

    exchange.EnqueueNewOrderResponse(Result<OrderDto, ExchangeErrCodeEnum>.Success(new OrderDto
    {
      Id = "dust-sell-1",
      Market = _btc,
      Side = OrderSide.Sell,
      Type = OrderType.Market,
      Status = OrderStatus.Filled,
      Amount = 0.001m,
      AmountFilled = 0.001m,
      AmountRemaining = 0,
    }));

    var orders = new[]
    {
      new OrderReqDto { Market = _btc, Side = OrderSide.Sell, Type = OrderType.Limit, Amount = 1 },
    };

    // Act
    var results = await _service.Rebalance(exchange, _credentials, orders, "Test");

    // Assert
    Assert.AreEqual(2, results.Length);
    Assert.IsTrue(results[0].IsSuperseded);
    Assert.IsFalse(results[1].IsSuperseded);

    Assert.AreEqual(2, exchange.NewOrderCalls.Count);
    Assert.AreEqual(0.001m, exchange.NewOrderCalls[1].Amount);
    Assert.IsNull(exchange.NewOrderCalls[1].AmountQuote);
    Assert.AreEqual(OrderType.Market, exchange.NewOrderCalls[1].Type);
  }

  [TestMethod]
  public async Task Rebalance_LimitBuyCancelledUnfilled_DustRemainder_IsDropped()
  {
    // Arrange
    // A dust buy remainder has no "liquidate the position" escape hatch the way a sell does —
    // there's nothing existing to fully acquire — so it's simply dropped rather than attempted.
    var exchange = new ScriptedExchange { MinOrderSizeInQuote = 1 };
    exchange.SetBestBidAsk("BTC", bid: 100, ask: 101);

    exchange.EnqueueNewOrderResponse(Result<OrderDto, ExchangeErrCodeEnum>.Success(new OrderDto
    {
      Id = "limit-1",
      Market = _btc,
      Side = OrderSide.Buy,
      Type = OrderType.Limit,
      Status = OrderStatus.New,
      Amount = 1,
      AmountFilled = 0,
      AmountRemaining = 1,
    }));

    exchange.EnqueueGetOrderResponse(new OrderDto
    {
      Id = "limit-1",
      Market = _btc,
      Side = OrderSide.Buy,
      Type = OrderType.Limit,
      Status = OrderStatus.Canceled,
      Amount = 1,
      AmountFilled = 0.999m,
      AmountRemaining = 0.001m,
    });

    var orders = new[]
    {
      new OrderReqDto { Market = _btc, Side = OrderSide.Buy, Type = OrderType.Limit, AmountQuote = 100 },
    };

    // Act
    var results = await _service.Rebalance(exchange, _credentials, orders, "Test");

    // Assert
    Assert.AreEqual(1, results.Length); // No fallback order placed for the dust remainder.
    Assert.IsTrue(results[0].IsSuperseded);
    Assert.AreEqual(1, exchange.NewOrderCalls.Count); // Only the original limit order.
  }

  [TestMethod]
  public async Task Rebalance_NoBestBidAskAvailable_FallsBackToMarketDirectly()
  {
    // Arrange — no SetBestBidAsk call, so GetBestBidAsk reports no book data for this market.
    var exchange = new ScriptedExchange { MinOrderSizeInQuote = 1 };

    exchange.EnqueueNewOrderResponse(Result<OrderDto, ExchangeErrCodeEnum>.Success(new OrderDto
    {
      Id = "market-1",
      Market = _btc,
      Side = OrderSide.Sell,
      Type = OrderType.Market,
      Status = OrderStatus.Filled,
      AmountQuote = 50,
      AmountQuoteFilled = 50,
    }));

    var orders = new[]
    {
      new OrderReqDto { Market = _btc, Side = OrderSide.Sell, Type = OrderType.Limit, AmountQuote = 50 },
    };

    // Act
    var results = await _service.Rebalance(exchange, _credentials, orders, "Test");

    // Assert
    Assert.AreEqual(1, results.Length);
    Assert.IsFalse(results[0].IsSuperseded); // No limit attempt was ever made.
    Assert.AreEqual(OrderType.Market, exchange.NewOrderCalls.Single().Type);
  }
}
