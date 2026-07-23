using Microsoft.Extensions.Logging.Abstractions;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Exchanges;
using TraderEngine.Common.Models;
using TraderEngine.Common.Services;
using TraderEngine.Common.Tests.Exchanges;

namespace TraderEngine.Common.Tests.Services;

/// <summary>
/// Covers <see cref="RebalancingService.Rebalance(Exchanges.IExchange, ConfigReqDto, IEnumerable{AbsAllocReqDto}, Balance?, string)"/>,
/// i.e. the core rebalance logic that computes allocation diffs against target percentages and
/// sells overages before buying underages. Uses <see cref="MockExchange"/> so orders actually
/// mutate the given <see cref="Balance"/>, allowing assertions on both the returned orders and
/// the resulting balance state.
/// </summary>
[TestClass]
public class RebalancingServiceTests
{
  private static readonly IRebalancingService _service = new RebalancingService(NullLogger<RebalancingService>.Instance);

  private static readonly MarketReqDto _eur = new("EUR", "EUR");
  private static readonly MarketReqDto _btc = new("EUR", "BTC");
  private static readonly MarketReqDto _eth = new("EUR", "ETH");
  private static readonly MarketReqDto _ada = new("EUR", "ADA");

  private static MockExchange NewExchange(
    Balance curBalance, decimal minOrderSize = 1, decimal makerFee = 0, decimal takerFee = 0)
  {
    return new("EUR", minOrderSize, makerFee, takerFee, curBalance);
  }

  // ── Basic overweight/underweight rebalancing ───────────────────────────────

  [TestMethod]
  public async Task Rebalance_OverweightAsset_SellsExactlyTheDiffToReachTarget()
  {
    // Arrange
    // BTC (300/400 = 75%) is overweight against its 50% target; ETH (100/400 = 25%) is
    // underweight against its 50% target. With zero fees the maths are exact: BTC should sell
    // exactly 100, funding ETH's exact 100 buy.
    var curBalance = new Balance("EUR");
    curBalance.TryAddAllocation(new Allocation(_eur, price: 1, amount: 0));
    curBalance.TryAddAllocation(new Allocation(_btc, price: 1, amount: 300));
    curBalance.TryAddAllocation(new Allocation(_eth, price: 1, amount: 100));

    var exchange = NewExchange(curBalance);

    var targets = new[]
    {
      new AbsAllocReqDto(_btc, .5m),
      new AbsAllocReqDto(_eth, .5m),
    };

    // Act
    var orders = await _service.Rebalance(exchange, new ConfigReqDto(), targets, curBalance);

    // Assert
    Assert.AreEqual(2, orders.Length);

    Assert.AreEqual(OrderSide.Sell, orders[0].Side);
    Assert.AreEqual("BTC", orders[0].Market.BaseSymbol);
    Assert.AreEqual(100m, orders[0].AmountQuoteFilled);
    Assert.IsNull(orders[0].Amount);

    Assert.AreEqual(OrderSide.Buy, orders[1].Side);
    Assert.AreEqual("ETH", orders[1].Market.BaseSymbol);
    Assert.AreEqual(100m, orders[1].AmountQuoteFilled);
  }

  [TestMethod]
  public async Task Rebalance_AlreadyAtTarget_ProducesNoOrders()
  {
    // Arrange
    var curBalance = new Balance("EUR");
    curBalance.TryAddAllocation(new Allocation(_eur, price: 1, amount: 0));
    curBalance.TryAddAllocation(new Allocation(_btc, price: 1, amount: 500));
    curBalance.TryAddAllocation(new Allocation(_eth, price: 1, amount: 500));

    var exchange = NewExchange(curBalance);

    var targets = new[]
    {
      new AbsAllocReqDto(_btc, .5m),
      new AbsAllocReqDto(_eth, .5m),
    };

    // Act
    var orders = await _service.Rebalance(exchange, new ConfigReqDto(), targets, curBalance);

    // Assert
    Assert.AreEqual(0, orders.Length);
  }

  [TestMethod]
  public async Task Rebalance_NewAssetNotCurrentlyHeld_IsBoughtFromZero()
  {
    // Arrange
    var curBalance = new Balance("EUR");
    curBalance.TryAddAllocation(new Allocation(_eur, price: 1, amount: 1000));

    var exchange = NewExchange(curBalance);

    var targets = new[] { new AbsAllocReqDto(_btc, 1m) };

    // Act
    var orders = await _service.Rebalance(exchange, new ConfigReqDto(), targets, curBalance);

    // Assert
    Assert.AreEqual(1, orders.Length);
    Assert.AreEqual(OrderSide.Buy, orders[0].Side);
    Assert.AreEqual("BTC", orders[0].Market.BaseSymbol);
    Assert.AreEqual(1000m, orders[0].AmountQuoteFilled);
  }

  [TestMethod]
  public async Task Rebalance_HeldAssetDroppedFromTargetList_IsFullyLiquidated()
  {
    // Arrange
    // BTC is held but absent from the target list entirely — its target share is 0, and what
    // would remain (0) is below MinOrderSizeInQuote, so the whole position is sold via Amount
    // instead of leaving unsellable dust.
    var curBalance = new Balance("EUR");
    curBalance.TryAddAllocation(new Allocation(_eur, price: 1, amount: 0));
    curBalance.TryAddAllocation(new Allocation(_btc, price: 1, amount: 200));

    var exchange = NewExchange(curBalance, minOrderSize: 5);

    var targets = new[] { new AbsAllocReqDto(_eth, 1m) };

    // Act
    var orders = await _service.Rebalance(exchange, new ConfigReqDto(), targets, curBalance);

    // Assert
    Assert.AreEqual(2, orders.Length);

    var sell = orders[0];
    Assert.AreEqual(OrderSide.Sell, sell.Side);
    Assert.AreEqual("BTC", sell.Market.BaseSymbol);
    Assert.AreEqual(200m, sell.Amount); // full position sold by Amount, not by AmountQuote
    Assert.IsNull(sell.AmountQuote);
    Assert.AreEqual(200m, sell.AmountQuoteFilled);

    var buy = orders[1];
    Assert.AreEqual(OrderSide.Buy, buy.Side);
    Assert.AreEqual("ETH", buy.Market.BaseSymbol);
    Assert.AreEqual(200m, buy.AmountQuoteFilled);
  }

  // ── Quote currency handling ─────────────────────────────────────────────────

  [TestMethod]
  public async Task Rebalance_NeverGeneratesAnOrderForTheQuoteCurrencyItself()
  {
    // Arrange
    // EUR is explicitly included in the target list at 50%, which yields a large (negative)
    // quote diff, but no order can ever be placed to trade EUR for EUR.
    var curBalance = new Balance("EUR");
    curBalance.TryAddAllocation(new Allocation(_eur, price: 1, amount: 100));
    curBalance.TryAddAllocation(new Allocation(_btc, price: 1, amount: 900));

    var exchange = NewExchange(curBalance);

    var targets = new[]
    {
      new AbsAllocReqDto(_eur, .5m),
      new AbsAllocReqDto(_btc, .5m),
    };

    // Act
    var orders = await _service.Rebalance(exchange, new ConfigReqDto(), targets, curBalance);

    // Assert
    Assert.AreEqual(1, orders.Length);
    CollectionAssert.DoesNotContain(orders.Select(o => o.Market.BaseSymbol).ToList(), "EUR");
  }

  [TestMethod]
  public async Task Rebalance_QuoteAllocationConfig_ReservesPercentageAsCash()
  {
    // Arrange
    // A 20% QuoteAllocation means BTC (targeted at 100% of the "investable" pool) should only
    // end up with 80% of the total portfolio — the remaining 20% stays as EUR.
    var curBalance = new Balance("EUR");
    curBalance.TryAddAllocation(new Allocation(_eur, price: 1, amount: 0));
    curBalance.TryAddAllocation(new Allocation(_btc, price: 1, amount: 1000));

    var exchange = NewExchange(curBalance);

    var config = new ConfigReqDto { QuoteAllocation = 20 };

    var targets = new[] { new AbsAllocReqDto(_btc, 1m) };

    // Act
    var orders = await _service.Rebalance(exchange, config, targets, curBalance);

    // Assert
    Assert.AreEqual(1, orders.Length);
    Assert.AreEqual(OrderSide.Sell, orders[0].Side);
    Assert.AreEqual(200m, orders[0].AmountQuoteFilled);

    Assert.AreEqual(200m, curBalance.GetAllocation("EUR")!.AmountQuote);
    Assert.AreEqual(800m, curBalance.GetAllocation("BTC")!.AmountQuote);
  }

  [TestMethod]
  public async Task Rebalance_QuoteTakeoutConfig_ReservesAbsoluteAmountAsCash()
  {
    // Arrange
    // A QuoteTakeout of 300 (absolute, in quote currency) must be carved out before the
    // remaining pool is distributed to targets.
    var curBalance = new Balance("EUR");
    curBalance.TryAddAllocation(new Allocation(_eur, price: 1, amount: 0));
    curBalance.TryAddAllocation(new Allocation(_btc, price: 1, amount: 1000));

    var exchange = NewExchange(curBalance);

    var config = new ConfigReqDto { QuoteTakeout = 300 };

    var targets = new[] { new AbsAllocReqDto(_btc, 1m) };

    // Act
    var orders = await _service.Rebalance(exchange, config, targets, curBalance);

    // Assert
    Assert.AreEqual(1, orders.Length);
    Assert.AreEqual(300m, orders[0].AmountQuoteFilled);

    Assert.AreEqual(300m, curBalance.GetAllocation("EUR")!.AmountQuote);
    Assert.AreEqual(700m, curBalance.GetAllocation("BTC")!.AmountQuote);
  }

  // ── Non-tradable (halted) market status ─────────────────────────────────────

  [TestMethod]
  public async Task Rebalance_HaltedNewAssetTarget_IsExcludedEntirely_DoesNotDiluteOthers()
  {
    // Arrange
    // XYZ is targeted at 50% but is Halted and not currently held — it must be excluded from
    // normalization entirely (not just skipped for trading), so ADA effectively gets 100%
    // instead of being diluted to 50%.
    var curBalance = new Balance("EUR");
    curBalance.TryAddAllocation(new Allocation(_eur, price: 1, amount: 0));
    curBalance.TryAddAllocation(new Allocation(_ada, price: 1, amount: 1000));

    var exchange = NewExchange(curBalance);

    var targets = new[]
    {
      new AbsAllocReqDto(_ada, .5m) { MarketStatus = MarketStatus.Trading },
      new AbsAllocReqDto(new MarketReqDto("EUR", "XYZ"), .5m) { MarketStatus = MarketStatus.Halted },
    };

    // Act
    var orders = await _service.Rebalance(exchange, new ConfigReqDto(), targets, curBalance);

    // Assert
    Assert.AreEqual(0, orders.Length);
  }

  [TestMethod]
  public async Task Rebalance_HaltedHeldAsset_IsLeftUntouched_ButStillConsumesNormalizationWeight()
  {
    // Arrange
    // BTC is currently held and targeted at 50%, but Halted — it cannot be traded, so it is
    // left exactly as-is (not sold down to its target), while ETH (targeted at 50%, Trading)
    // still only sees 50% of the pool. With no quote currency available at all, ETH's buy is
    // completely starved (ratio = 0).
    var curBalance = new Balance("EUR");
    curBalance.TryAddAllocation(new Allocation(_eur, price: 1, amount: 0));
    curBalance.TryAddAllocation(new Allocation(_btc, price: 1, amount: 800));
    curBalance.TryAddAllocation(new Allocation(_eth, price: 1, amount: 200));

    var exchange = NewExchange(curBalance);

    var targets = new[]
    {
      new AbsAllocReqDto(_btc, .5m) { MarketStatus = MarketStatus.Halted },
      new AbsAllocReqDto(_eth, .5m) { MarketStatus = MarketStatus.Trading },
    };

    // Act
    var orders = await _service.Rebalance(exchange, new ConfigReqDto(), targets, curBalance);

    // Assert
    Assert.AreEqual(0, orders.Length);
    Assert.AreEqual(800m, curBalance.GetAllocation("BTC")!.AmountQuote);
  }

  [TestMethod]
  public async Task Rebalance_BuyRatioScaling_ProportionallyScalesMultipleBuyOrdersWhenQuoteInsufficient()
  {
    // Arrange
    // BTC is frozen (Halted, held, targeted at 50%), leaving only 50 EUR of actual free cash
    // for ETH (targeted 30%, wants +200) and ADA (targeted 20%, wants +150). The combined
    // demand of 350 exceeds the 50 available, so both buys are scaled down by the same ratio
    // (50 / 350 = 1/7).
    var curBalance = new Balance("EUR");
    curBalance.TryAddAllocation(new Allocation(_eur, price: 1, amount: 50));
    curBalance.TryAddAllocation(new Allocation(_btc, price: 1, amount: 800));
    curBalance.TryAddAllocation(new Allocation(_eth, price: 1, amount: 100));
    curBalance.TryAddAllocation(new Allocation(_ada, price: 1, amount: 50));

    var exchange = NewExchange(curBalance);

    var targets = new[]
    {
      new AbsAllocReqDto(_btc, .5m) { MarketStatus = MarketStatus.Halted },
      new AbsAllocReqDto(_eth, .3m) { MarketStatus = MarketStatus.Trading },
      new AbsAllocReqDto(_ada, .2m) { MarketStatus = MarketStatus.Trading },
    };

    // Act
    var orders = await _service.Rebalance(exchange, new ConfigReqDto(), targets, curBalance);

    // Assert
    Assert.AreEqual(2, orders.Length);

    Assert.IsTrue(orders.All(o => o.Side == OrderSide.Buy));
    CollectionAssert.DoesNotContain(orders.Select(o => o.Market.BaseSymbol).ToList(), "BTC");

    var eth = orders.Single(o => o.Market.BaseSymbol == "ETH");
    var ada = orders.Single(o => o.Market.BaseSymbol == "ADA");

    Assert.AreEqual(28.57m, eth.AmountQuoteFilled);
    Assert.AreEqual(21.42m, ada.AmountQuoteFilled);

    Assert.AreEqual(800m, curBalance.GetAllocation("BTC")!.AmountQuote);
  }

  // ── Dust and minimum order size interactions ────────────────────────────────

  [TestMethod]
  public async Task Rebalance_TinyResidualBuyAfterFullLiquidation_IsFilteredByMinimumOrderSize()
  {
    // Arrange
    // BTC has a small (3%) but nonzero target, and what would remain after a partial sell
    // (3.18) is below MinOrderSizeInQuote (5), so BTC is fully liquidated via Amount. The
    // liquidation then leaves BTC's own tiny 3% re-normalization diff (3.18) on the buy pass,
    // but that too falls below MinOrderSizeInQuote and is filtered out — so BTC does not get
    // an unwanted tiny re-purchase. ETH (97%) is bought with the rest.
    var curBalance = new Balance("EUR");
    curBalance.TryAddAllocation(new Allocation(_eur, price: 1, amount: 0));
    curBalance.TryAddAllocation(new Allocation(_btc, price: 1, amount: 106));

    var exchange = NewExchange(curBalance, minOrderSize: 5);

    var targets = new[]
    {
      new AbsAllocReqDto(_btc, .03m),
      new AbsAllocReqDto(_eth, .97m),
    };

    // Act
    var orders = await _service.Rebalance(exchange, new ConfigReqDto(), targets, curBalance);

    // Assert
    Assert.AreEqual(2, orders.Length);

    var sell = orders[0];
    Assert.AreEqual(OrderSide.Sell, sell.Side);
    Assert.AreEqual("BTC", sell.Market.BaseSymbol);
    Assert.AreEqual(106m, sell.Amount);

    var buy = orders[1];
    Assert.AreEqual(OrderSide.Buy, buy.Side);
    Assert.AreEqual("ETH", buy.Market.BaseSymbol);
    Assert.AreEqual(102.82m, buy.AmountQuoteFilled);

    CollectionAssert.DoesNotContain(
      orders.Where(o => o.Side == OrderSide.Buy).Select(o => o.Market.BaseSymbol).ToList(), "BTC");
  }

  // ── Numeric edge cases ───────────────────────────────────────────────────────

  [TestMethod]
  public async Task Rebalance_FullQuoteAllocation_LiquidatesEverythingToCash()
  {
    // Arrange
    // QuoteAllocation = 100 makes quoteRelAlloc clamp to exactly 1, hitting the explicit
    // div == 0 special case in GetAllocationQuoteDiffs (totalAbsAlloc forced to 0) rather than
    // dividing by zero. Every target's relative allocation becomes 0, so everything held gets
    // sold down to cash regardless of what's targeted.
    var curBalance = new Balance("EUR");
    curBalance.TryAddAllocation(new Allocation(_eur, price: 1, amount: 0));
    curBalance.TryAddAllocation(new Allocation(_btc, price: 1, amount: 1000));

    var exchange = NewExchange(curBalance, minOrderSize: 5);

    var config = new ConfigReqDto { QuoteAllocation = 100 };

    var targets = new[] { new AbsAllocReqDto(_btc, 1m) };

    // Act
    var orders = await _service.Rebalance(exchange, config, targets, curBalance);

    // Assert
    Assert.AreEqual(1, orders.Length);
    Assert.AreEqual(OrderSide.Sell, orders[0].Side);
    Assert.AreEqual(1000m, orders[0].Amount); // full liquidation, remaining target is 0

    Assert.AreEqual(1000m, curBalance.GetAllocation("EUR")!.AmountQuote);
    Assert.AreEqual(0m, curBalance.GetAllocation("BTC")!.AmountQuote);
  }

  [TestMethod]
  public async Task Rebalance_ZeroTotalPortfolioValue_ProducesNoOrders()
  {
    // Arrange
    // An empty portfolio (just a zero EUR balance) must not divide by zero or otherwise crash
    // when computing target amounts against a total of 0.
    var curBalance = new Balance("EUR");
    curBalance.TryAddAllocation(new Allocation(_eur, price: 1, amount: 0));

    var exchange = NewExchange(curBalance);

    var targets = new[] { new AbsAllocReqDto(_btc, 1m) };

    // Act
    var orders = await _service.Rebalance(exchange, new ConfigReqDto(), targets, curBalance);

    // Assert
    Assert.AreEqual(0, orders.Length);
  }

  [TestMethod]
  public async Task Rebalance_DiffExactlyAtMinimumOrderSize_IsNotTreatedAsDust()
  {
    // Arrange
    // BTC's target leaves exactly MinOrderSizeInQuote (5) of remaining value after the sell.
    // The dust-prevention check is a strict "<", so a remainder exactly at the threshold must
    // still be sold as a normal partial (AmountQuote-based) sell, not liquidated in full.
    var curBalance = new Balance("EUR");
    curBalance.TryAddAllocation(new Allocation(_eur, price: 1, amount: 0));
    curBalance.TryAddAllocation(new Allocation(_btc, price: 1, amount: 100));

    var exchange = NewExchange(curBalance, minOrderSize: 5);

    // QuoteAllocation = 95 -> relAlloc = 0.05 -> target = 5 (of a 100 total) -> remaining = 5.
    var config = new ConfigReqDto { QuoteAllocation = 95 };

    var targets = new[] { new AbsAllocReqDto(_btc, 1m) };

    // Act
    var orders = await _service.Rebalance(exchange, config, targets, curBalance);

    // Assert
    Assert.AreEqual(1, orders.Length);
    Assert.AreEqual(95m, orders[0].AmountQuoteFilled);
    Assert.IsNull(orders[0].Amount); // partial sell, not a full-position dust liquidation
  }

  [TestMethod]
  public async Task Rebalance_DustLiquidation_UnknownAssetPrecision_SellsFullUnroundedAmount()
  {
    // Arrange
    // When the exchange has no known decimals precision for an asset, the dust-liquidation
    // branch must fall back to the unrounded allocation amount instead of flooring to a
    // precision it doesn't actually know.
    var oneThird = 1m / 3m; // 0.3333333333333333333333333333 (28 significant digits)

    var curBalance = new Balance("EUR");
    curBalance.TryAddAllocation(new Allocation(_eur, price: 1, amount: 0));
    curBalance.TryAddAllocation(new Allocation(_btc, price: 1, amount: oneThird));

    var exchange = new NullDecimalsExchange("EUR", minOrderSize: 5, makerFee: 0, takerFee: 0, curBalance);

    // BTC is absent from the target list -> full liquidation via the dust branch.
    var targets = new[] { new AbsAllocReqDto(_eth, 1m) };

    // Act
    var orders = await _service.Rebalance(exchange, new ConfigReqDto(), targets, curBalance);

    // Assert
    var sell = orders.Single(o => o.Market.BaseSymbol == "BTC");
    Assert.AreEqual(oneThird, sell.Amount); // full precision preserved, not floored to 8 decimals
  }

  // ── Realistic multi-asset regression ────────────────────────────────────────

  [TestMethod]
  public async Task Rebalance_MultiAssetPortfolioWithFeesAndPriceDrift_SellsWholeUntargetedPosition()
  {
    // Arrange
    // A realistic portfolio where prices have drifted since acquisition: BTC and ETH have
    // appreciated, BNB has depreciated. BNB is dropped from the target list in favor of ADA.
    // This is a regression test preserving a previously known-good result (migrated from
    // TraderEngine.API.Tests/Extensions/TraderTests.cs).
    var curBalance = new Balance("EUR");
    curBalance.TryAddAllocation(new Allocation(_eur, price: 1, amount: 50));
    curBalance.TryAddAllocation(new Allocation(_btc, price: 18_000, amount: .40m * 1000 / 15_000));
    curBalance.TryAddAllocation(new Allocation(_eth, price: 1_610, amount: .30m * 1000 / 1_400));
    curBalance.TryAddAllocation(new Allocation(new MarketReqDto("EUR", "BNB"), price: 306, amount: .25m * 1000 / 340));

    var exchange = NewExchange(curBalance, minOrderSize: 5, makerFee: .0015m, takerFee: .0025m);

    var targets = new[]
    {
      new AbsAllocReqDto(_eur, .05m),
      new AbsAllocReqDto(_btc, .40m),
      new AbsAllocReqDto(_eth, .30m),
      new AbsAllocReqDto(_ada, .25m),
    };

    // Act
    var orders = (await _service.Rebalance(exchange, new ConfigReqDto(), targets, curBalance)).ToList();

    // Assert
    Assert.AreEqual(4, orders.Count);

    Assert.AreEqual(1.3872m, Math.Round(orders.Sum(result => result.FeePaid), 4));

    Assert.IsNull(orders[0].Amount);
    Assert.IsNull(orders[1].Amount);
    Assert.IsNotNull(orders[2].Amount); // BNB dropped from target list — expected to sell whole position
    Assert.IsNull(orders[3].Amount);
  }
}
