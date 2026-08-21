using Microsoft.Extensions.Logging.Abstractions;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Exchanges;
using TraderEngine.Common.Models;
using TraderEngine.Common.Services;
using TraderEngine.Common.Tests.Exchanges;

namespace TraderEngine.Common.Tests.Services;

/// <summary>
/// Covers <see cref="RebalancingService.Rebalance(Exchanges.IExchange, ConfigReqDto, IEnumerable{TargetAllocReqDto}, Balance?, string)"/>,
/// i.e. the core rebalance logic that computes allocation diffs against target percentages and
/// sells overages before buying underages. Uses <see cref="MockExchange"/> so orders actually
/// mutate the given <see cref="Balance"/>, allowing assertions on both the returned orders and
/// the resulting balance state.
/// </summary>
[TestClass]
public class RebalancingServiceTests
{
  private static readonly IRebalancingService _service = new RebalancingService(NullLogger<RebalancingService>.Instance);

  private static readonly ExchangeCredentials _credentials = new("test-key", "test-secret");

  private static readonly MarketReqDto _eur = new("EUR", "EUR");
  private static readonly MarketReqDto _btc = new("EUR", "BTC");
  private static readonly MarketReqDto _eth = new("EUR", "ETH");
  private static readonly MarketReqDto _ada = new("EUR", "ADA");
  private static readonly MarketReqDto _sol = new("EUR", "SOL");

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
      new TargetAllocReqDto(_btc, .5m),
      new TargetAllocReqDto(_eth, .5m),
    };

    // Act
    var orders = await _service.Rebalance(exchange, _credentials, new ConfigReqDto(), targets, curBalance);

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
  public async Task Rebalance_UseLimitOrdersEnabled_SimulatesAsLimitOrders_AtMakerFee()
  {
    // Arrange
    // RebalancingService itself has no notion of "simulation" — it always honors
    // ConfigReqDto.UseLimitOrders as given. MockExchange (used for both real dry-run previews and
    // this test) resolves a Limit order's price from the cached allocation price rather than a
    // real order book, and fills it instantly — so the preview stays accurate (maker fee, correct
    // Type on the result) without ever touching a real exchange for a placement that will never
    // actually rest.
    var curBalance = new Balance("EUR");
    curBalance.TryAddAllocation(new Allocation(_eur, price: 1, amount: 0));
    curBalance.TryAddAllocation(new Allocation(_btc, price: 1, amount: 300));
    curBalance.TryAddAllocation(new Allocation(_eth, price: 1, amount: 100));

    var exchange = NewExchange(curBalance, minOrderSize: 1, makerFee: 0.0015m, takerFee: 0.0025m);

    var targets = new[]
    {
      new TargetAllocReqDto(_btc, .5m),
      new TargetAllocReqDto(_eth, .5m),
    };

    var config = new ConfigReqDto { UseLimitOrders = true };

    // Act
    var orders = await _service.Rebalance(exchange, _credentials, config, targets, curBalance);

    // Assert
    Assert.AreEqual(2, orders.Length);
    Assert.IsTrue(orders.All(order => order.Type == OrderType.Limit));
    Assert.IsTrue(orders.All(order => !order.IsSuperseded)); // MockExchange always fills outright.

    // Sell of 100 BTC (@1) at the 0.15% maker rate, not the 0.25% taker rate.
    Assert.AreEqual(100m * 0.0015m, orders[0].FeePaid);
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
      new TargetAllocReqDto(_btc, .5m),
      new TargetAllocReqDto(_eth, .5m),
    };

    // Act
    var orders = await _service.Rebalance(exchange, _credentials, new ConfigReqDto(), targets, curBalance);

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

    var targets = new[] { new TargetAllocReqDto(_btc, 1m) };

    // Act
    var orders = await _service.Rebalance(exchange, _credentials, new ConfigReqDto(), targets, curBalance);

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

    var targets = new[] { new TargetAllocReqDto(_eth, 1m) };

    // Act
    var orders = await _service.Rebalance(exchange, _credentials, new ConfigReqDto(), targets, curBalance);

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
      new TargetAllocReqDto(_eur, .5m),
      new TargetAllocReqDto(_btc, .5m),
    };

    // Act
    var orders = await _service.Rebalance(exchange, _credentials, new ConfigReqDto(), targets, curBalance);

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

    var targets = new[] { new TargetAllocReqDto(_btc, 1m) };

    // Act
    var orders = await _service.Rebalance(exchange, _credentials, config, targets, curBalance);

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

    var targets = new[] { new TargetAllocReqDto(_btc, 1m) };

    // Act
    var orders = await _service.Rebalance(exchange, _credentials, config, targets, curBalance);

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
      new TargetAllocReqDto(_ada, .5m) { MarketStatus = MarketStatus.Trading },
      new TargetAllocReqDto(new MarketReqDto("EUR", "XYZ"), .5m) { MarketStatus = MarketStatus.Halted },
    };

    // Act
    var orders = await _service.Rebalance(exchange, _credentials, new ConfigReqDto(), targets, curBalance);

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
      new TargetAllocReqDto(_btc, .5m) { MarketStatus = MarketStatus.Halted },
      new TargetAllocReqDto(_eth, .5m) { MarketStatus = MarketStatus.Trading },
    };

    // Act
    var orders = await _service.Rebalance(exchange, _credentials, new ConfigReqDto(), targets, curBalance);

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
      new TargetAllocReqDto(_btc, .5m) { MarketStatus = MarketStatus.Halted },
      new TargetAllocReqDto(_eth, .3m) { MarketStatus = MarketStatus.Trading },
      new TargetAllocReqDto(_ada, .2m) { MarketStatus = MarketStatus.Trading },
    };

    // Act
    var orders = await _service.Rebalance(exchange, _credentials, new ConfigReqDto(), targets, curBalance);

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

  [TestMethod]
  public async Task Rebalance_BuyRatioScaling_WithTakerFee_ReservesEachLegsOwnFeeFromItsOwnClaim()
  {
    // Arrange
    // Same shape as Rebalance_BuyRatioScaling_ProportionallyScalesMultipleBuyOrdersWhenQuoteInsufficient,
    // but with a non-zero TakerFee. Bitvavo charges a buy order's fee in quote currency IN ADDITION
    // to its trade value, drawn from the same EUR pool — so once the batch-wide ratio has already
    // scaled ETH and ADA's combined trade values down to fit the available-with-buffer pool, there
    // is nothing left over to also cover their own fees. The running ledger this test exercises
    // closes that gap per leg, in claim order: ETH (processed first) claims its full ratio-scaled
    // share since there's room; ADA (processed second) then finds less left in the ledger than its
    // own ratio-scaled share, because ETH's claim already reserved its own fee out of the shared
    // pool, so ADA's claim is clamped further, below what the naive ratio-only maths would give it.
    var curBalance = new Balance("EUR");
    curBalance.TryAddAllocation(new Allocation(_eur, price: 1, amount: 50));
    curBalance.TryAddAllocation(new Allocation(_btc, price: 1, amount: 800));
    curBalance.TryAddAllocation(new Allocation(_eth, price: 1, amount: 100));
    curBalance.TryAddAllocation(new Allocation(_ada, price: 1, amount: 50));

    var exchange = NewExchange(curBalance, minOrderSize: 1, makerFee: 0, takerFee: 0.0025m);

    var targets = new[]
    {
      new TargetAllocReqDto(_btc, .5m) { MarketStatus = MarketStatus.Halted },
      new TargetAllocReqDto(_eth, .3m) { MarketStatus = MarketStatus.Trading },
      new TargetAllocReqDto(_ada, .2m) { MarketStatus = MarketStatus.Trading },
    };

    // Act
    var orders = await _service.Rebalance(exchange, _credentials, new ConfigReqDto(), targets, curBalance);

    // Assert
    Assert.AreEqual(2, orders.Length);

    var eth = orders.Single(o => o.Market.BaseSymbol == "ETH");
    var ada = orders.Single(o => o.Market.BaseSymbol == "ADA");

    // ratio = min(350, 49.875 / 1.0025) / 350 already reserves each leg's own fee inside the
    // ratio itself, so both legs claim their full ratio-scaled share unclamped by the ledger.
    Assert.AreEqual(28.42m, eth.AmountQuoteFilled);
    Assert.AreEqual(21.32m, ada.AmountQuoteFilled);

    // The invariant the ledger enforces: no combination of trade value + each leg's own taker fee
    // ever draws more than the 50 EUR that was actually available before either order was placed.
    var totalDrawnIncludingFees = orders.Sum(o => o.AmountQuoteFilled * (1 + exchange.TakerFee));
    Assert.IsTrue(totalDrawnIncludingFees <= 50m);
  }

  [TestMethod]
  public async Task Rebalance_BuyLedgerClamp_DropsLegEntirelyWhenClaimFallsBelowMinimum()
  {
    // Arrange
    // BTC is frozen (Halted, held, weighted to soak up the rest of the normalization total) so it
    // generates no order of its own regardless of its target. Target weights are set equal to the
    // exact desired quote amounts for two brand-new (not currently held) positions — with
    // totalTargetWeight equal to curBalance.AmountQuoteTotal (810), each target's relative
    // allocation collapses to exactly its own weight — so ETH's drift is exactly 195 and ADA's is
    // exactly 5. Against only 10 EUR available, the ratio scales both down proportionally, but
    // ADA's already-small share shrinks further still once ETH's own reserved fee is taken out of
    // the shared ledger first (ETH is processed first) — enough to push what's left for ADA under
    // MinOrderSizeInQuote entirely, not just smaller.
    var curBalance = new Balance("EUR");
    curBalance.TryAddAllocation(new Allocation(_eur, price: 1, amount: 10));
    curBalance.TryAddAllocation(new Allocation(_btc, price: 1, amount: 800));

    var exchange = NewExchange(curBalance, minOrderSize: 1, makerFee: 0, takerFee: 0.0025m);

    var targets = new[]
    {
      new TargetAllocReqDto(_btc, 610m) { MarketStatus = MarketStatus.Halted },
      new TargetAllocReqDto(_eth, 195m) { MarketStatus = MarketStatus.Trading },
      new TargetAllocReqDto(_ada, 5m) { MarketStatus = MarketStatus.Trading },
    };

    // Act
    var orders = await _service.Rebalance(exchange, _credentials, new ConfigReqDto(), targets, curBalance);

    // Assert
    Assert.AreEqual(1, orders.Length); // ADA claimed too little to clear the exchange minimum.

    var eth = orders.Single(o => o.Market.BaseSymbol == "ETH");
    Assert.AreEqual(9.7m, eth.AmountQuoteFilled);

    CollectionAssert.DoesNotContain(orders.Select(o => o.Market.BaseSymbol).ToList(), "ADA");
  }

  [TestMethod]
  public async Task Rebalance_BuyLedgerClamp_DroppedLegDoesNotStarveLaterLegs()
  {
    // Arrange
    // Three buy legs, processed in order ETH (drops below minimum after scaling), ADA (large,
    // dominant), SOL (small, survives). ETH's dropped claim must never actually be debited from
    // the shared ledger — the whole point of the regression this guards: a dropped leg's claim
    // used to be subtracted from the ledger regardless of whether its order was ever placed,
    // silently starving whichever leg came after it. SOL's expected claim below is computed via
    // the exact same formula production uses (an independent "fair share" oracle), so any future
    // leak — this one or the double fee-reservation one ratio itself now guards against — would
    // show up as SOL claiming strictly less than its true fair share, not just as a pass/fail flip.
    var curBalance = new Balance("EUR");
    curBalance.TryAddAllocation(new Allocation(_eur, price: 1, amount: 50));
    curBalance.TryAddAllocation(new Allocation(_btc, price: 1, amount: 800));

    var exchange = NewExchange(curBalance, minOrderSize: 5, makerFee: 0, takerFee: 0.0025m);

    const decimal ethRaw = 10m;
    const decimal adaRaw = 100m;
    const decimal solRaw = 14m;
    var totalQuote = curBalance.AmountQuoteTotal;
    var btcWeight = totalQuote - ethRaw - adaRaw - solRaw;

    var targets = new[]
    {
      new TargetAllocReqDto(_btc, btcWeight) { MarketStatus = MarketStatus.Halted },
      new TargetAllocReqDto(_eth, ethRaw) { MarketStatus = MarketStatus.Trading },
      new TargetAllocReqDto(_ada, adaRaw) { MarketStatus = MarketStatus.Trading },
      new TargetAllocReqDto(_sol, solRaw) { MarketStatus = MarketStatus.Trading },
    };

    // Independently-computed expected values, mirroring BuyUnderagesAndVerify's own formula.
    var availableWithFeeBuffer = curBalance.AmountQuoteAvailable * (1 - exchange.TakerFee);
    var totalBuy = ethRaw + adaRaw + solRaw;
    var ratio = Math.Min(totalBuy, availableWithFeeBuffer / (1 + exchange.TakerFee)) / totalBuy;
    var expectedEth = Math.Floor(ethRaw * ratio * 100) / 100;
    var expectedAda = Math.Floor(adaRaw * ratio * 100) / 100;
    var expectedSol = Math.Floor(solRaw * ratio * 100) / 100;

    Assert.IsTrue(expectedEth < 5m, "Test setup assumption: ETH's scaled claim must fall below the exchange minimum.");
    Assert.IsTrue(expectedSol >= 5m, "Test setup assumption: SOL's fair, unclamped claim must clear the exchange minimum.");

    // Act
    var orders = await _service.Rebalance(exchange, _credentials, new ConfigReqDto(), targets, curBalance);

    // Assert
    Assert.AreEqual(2, orders.Length); // ETH dropped; ADA and SOL both placed.

    CollectionAssert.DoesNotContain(orders.Select(o => o.Market.BaseSymbol).ToList(), "ETH");

    var ada = orders.Single(o => o.Market.BaseSymbol == "ADA");
    var sol = orders.Single(o => o.Market.BaseSymbol == "SOL");

    Assert.AreEqual(expectedAda, ada.AmountQuoteFilled);
    Assert.AreEqual(expectedSol, sol.AmountQuoteFilled); // Untouched by ETH's dropped, never-spent claim.
  }

  [TestMethod]
  public async Task Rebalance_BuyRatioScaling_ThreeEquallySizedLegs_AllReceiveEqualFairShare()
  {
    // Arrange
    // Three equally-sized buy legs (ETH, ADA, SOL each wanting 40) against 100 EUR available.
    // `ratio` itself already reserves each leg's own fee (divided by 1 + TakerFee before capping
    // the trade-value sum), so all three should claim the exact same fair, unclamped share — the
    // per-leg ledger should have nothing left to do here. This guards against the ratio and the
    // per-leg ledger reservation double-counting the fee (once in `ratio`, again per leg), which
    // would otherwise make whichever leg is processed last (SOL here) absorb the accumulated fee
    // reservations of every prior leg and get needlessly shorted purely due to enumeration order.
    var curBalance = new Balance("EUR");
    curBalance.TryAddAllocation(new Allocation(_eur, price: 1, amount: 100));
    curBalance.TryAddAllocation(new Allocation(_btc, price: 1, amount: 800));
    curBalance.TryAddAllocation(new Allocation(_eth, price: 1, amount: 100));
    curBalance.TryAddAllocation(new Allocation(_ada, price: 1, amount: 100));
    curBalance.TryAddAllocation(new Allocation(_sol, price: 1, amount: 100));

    var exchange = NewExchange(curBalance, minOrderSize: 1, makerFee: 0, takerFee: 0.0025m);

    var targets = new[]
    {
      new TargetAllocReqDto(_btc, .4m) { MarketStatus = MarketStatus.Halted },
      new TargetAllocReqDto(_eth, .2m) { MarketStatus = MarketStatus.Trading },
      new TargetAllocReqDto(_ada, .2m) { MarketStatus = MarketStatus.Trading },
      new TargetAllocReqDto(_sol, .2m) { MarketStatus = MarketStatus.Trading },
    };

    // Act
    var orders = await _service.Rebalance(exchange, _credentials, new ConfigReqDto(), targets, curBalance);

    // Assert
    Assert.AreEqual(3, orders.Length);

    var eth = orders.Single(o => o.Market.BaseSymbol == "ETH");
    var ada = orders.Single(o => o.Market.BaseSymbol == "ADA");
    var sol = orders.Single(o => o.Market.BaseSymbol == "SOL");

    // All three equal, and unclamped by the ledger regardless of processing order.
    Assert.AreEqual(33.16m, eth.AmountQuoteFilled);
    Assert.AreEqual(33.16m, ada.AmountQuoteFilled);
    Assert.AreEqual(33.16m, sol.AmountQuoteFilled);

    var totalDrawnIncludingFees = orders.Sum(o => o.AmountQuoteFilled * (1 + exchange.TakerFee));
    Assert.IsTrue(totalDrawnIncludingFees <= 100m);
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
      new TargetAllocReqDto(_btc, .03m),
      new TargetAllocReqDto(_eth, .97m),
    };

    // Act
    var orders = await _service.Rebalance(exchange, _credentials, new ConfigReqDto(), targets, curBalance);

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
    // div == 0 special case in GetAllocationQuoteDrifts (totalTargetWeight forced to 0) rather than
    // dividing by zero. Every target's relative allocation becomes 0, so everything held gets
    // sold down to cash regardless of what's targeted.
    var curBalance = new Balance("EUR");
    curBalance.TryAddAllocation(new Allocation(_eur, price: 1, amount: 0));
    curBalance.TryAddAllocation(new Allocation(_btc, price: 1, amount: 1000));

    var exchange = NewExchange(curBalance, minOrderSize: 5);

    var config = new ConfigReqDto { QuoteAllocation = 100 };

    var targets = new[] { new TargetAllocReqDto(_btc, 1m) };

    // Act
    var orders = await _service.Rebalance(exchange, _credentials, config, targets, curBalance);

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

    var targets = new[] { new TargetAllocReqDto(_btc, 1m) };

    // Act
    var orders = await _service.Rebalance(exchange, _credentials, new ConfigReqDto(), targets, curBalance);

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

    var targets = new[] { new TargetAllocReqDto(_btc, 1m) };

    // Act
    var orders = await _service.Rebalance(exchange, _credentials, config, targets, curBalance);

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
    var targets = new[] { new TargetAllocReqDto(_eth, 1m) };

    // Act
    var orders = await _service.Rebalance(exchange, _credentials, new ConfigReqDto(), targets, curBalance);

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
      new TargetAllocReqDto(_eur, .05m),
      new TargetAllocReqDto(_btc, .40m),
      new TargetAllocReqDto(_eth, .30m),
      new TargetAllocReqDto(_ada, .25m),
    };

    // Act
    var orders = (await _service.Rebalance(exchange, _credentials, new ConfigReqDto(), targets, curBalance)).ToList();

    // Assert
    Assert.AreEqual(4, orders.Count);

    // 1.3872 under the old sequential design, which re-applied the settlement-lag TakerFee buffer
    // to the entire post-sell balance including sell proceeds ((initial + proceeds) * (1 - fee)).
    // The interleaved ledger tracks each sell's proceeds via its own authoritative
    // AmountQuoteFilled - FeePaid (already net, no settlement-lag ambiguity to buffer against) and
    // only buffers the initial pre-sell snapshot (initial * (1 - fee) + proceeds) — a legitimate
    // reduction in unnecessary double-discounting, letting ADA's buy claim a fraction more.
    Assert.AreEqual(1.3875m, Math.Round(orders.Sum(result => result.FeePaid), 4));

    Assert.IsNull(orders[0].Amount);
    Assert.IsNull(orders[1].Amount);
    Assert.IsNotNull(orders[2].Amount); // BNB dropped from target list — expected to sell whole position
    Assert.IsNull(orders[3].Amount);
  }
}
