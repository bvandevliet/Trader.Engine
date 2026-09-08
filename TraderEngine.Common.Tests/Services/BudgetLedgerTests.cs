using TraderEngine.Common.Services;

namespace TraderEngine.Common.Tests.Services;

/// <summary>
/// Covers <see cref="BudgetLedger"/> in isolation: the async wait/broadcast mechanics, the
/// all-or-nothing batch fast path, the fair proportional split in <see cref="BudgetLedger.Complete"/>,
/// its in-flight-aware reconciliation, and concurrent-claim safety under real parallelism (not just
/// the cooperative single-threaded interleaving the higher-level <c>RebalancingService</c> tests
/// exercise via synchronous test doubles).
/// </summary>
[TestClass]
public class BudgetLedgerTests
{
  [TestMethod]
  public async Task ClaimAsync_ThenTryResolvePendingBatch_EnoughAvailable_ClaimsImmediately()
  {
    var ledger = new BudgetLedger(100);

    var claimTask = ledger.ClaimAsync(40, 1);
    ledger.TryResolvePendingBatch();

    var claimed = await claimTask.WaitAsync(TimeSpan.FromSeconds(5));

    Assert.AreEqual(40, claimed);
  }

  [TestMethod]
  public async Task ClaimAsync_RequestBelowZero_ReturnsZero()
  {
    var ledger = new BudgetLedger(100);

    var claimed = await ledger.ClaimAsync(0, 1);

    Assert.AreEqual(0, claimed);
  }

  [TestMethod]
  public async Task ClaimAsync_NotResolvedUntilTryResolvePendingBatchIsCalled()
  {
    // A claim that's trivially affordable on its own still doesn't resolve just from being
    // requested — the caller must explicitly signal "the whole batch is registered, check now"
    // via TryResolvePendingBatch (see BudgetLedger's own remarks on why this isn't automatic).
    var ledger = new BudgetLedger(100);

    var claimTask = ledger.ClaimAsync(40, 1);

    await Task.Delay(20);
    Assert.IsFalse(claimTask.IsCompleted, "Test setup assumption: claim should not resolve on its own.");

    ledger.TryResolvePendingBatch();

    var claimed = await claimTask.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.AreEqual(40, claimed);
  }

  [TestMethod]
  public async Task ClaimAsync_BatchNotFullyCovered_NoneResolveUntilCovered()
  {
    // Two claims registered together; the pool covers neither individually nor both together yet
    // — resolving one alone (because it happens to be small) would let it cut in line ahead of the
    // other, which is exactly what the all-or-nothing batch check exists to prevent.
    var ledger = new BudgetLedger(15);

    // minClaimable 0 on both, so the pure proportional-split math is what's under test here, not
    // the separate "below minimum gets nothing" rule (covered by its own tests below).
    var smallClaim = ledger.ClaimAsync(5, 0);
    var largeClaim = ledger.ClaimAsync(100, 0);
    ledger.TryResolvePendingBatch();

    await Task.Delay(20);
    Assert.IsFalse(smallClaim.IsCompleted, "The small claim must not resolve alone ahead of the large one.");
    Assert.IsFalse(largeClaim.IsCompleted);

    ledger.Complete(null);

    var small = await smallClaim.WaitAsync(TimeSpan.FromSeconds(5));
    var large = await largeClaim.WaitAsync(TimeSpan.FromSeconds(5));

    // Fair split of the 15 available across a 5:100 ratio of demand.
    Assert.AreEqual(15m * 5 / 105, small);
    Assert.AreEqual(15m * 100 / 105, large);
  }

  [TestMethod]
  public async Task Deposit_CoversWholeBatch_ResolvesAllPendingClaimsTogether()
  {
    var ledger = new BudgetLedger(0);

    var claimA = ledger.ClaimAsync(10, 1);
    var claimB = ledger.ClaimAsync(20, 1);
    ledger.TryResolvePendingBatch();

    await Task.Delay(20);
    Assert.IsFalse(claimA.IsCompleted);
    Assert.IsFalse(claimB.IsCompleted);

    // Not yet enough for both together.
    ledger.Deposit(10);
    await Task.Delay(20);
    Assert.IsFalse(claimA.IsCompleted, "Test setup assumption: a partial deposit must not resolve either claim.");
    Assert.IsFalse(claimB.IsCompleted);

    // Now enough for both together.
    ledger.Deposit(20);

    Assert.AreEqual(10m, await claimA.WaitAsync(TimeSpan.FromSeconds(5)));
    Assert.AreEqual(20m, await claimB.WaitAsync(TimeSpan.FromSeconds(5)));
  }

  [TestMethod]
  public async Task ClaimAsync_CompleteWithNoDeposits_ClaimsWhateverIsLeftIfAboveMinimum()
  {
    var ledger = new BudgetLedger(10);

    var claimTask = ledger.ClaimAsync(30, 5);
    ledger.TryResolvePendingBatch();

    await Task.Delay(20);
    Assert.IsFalse(claimTask.IsCompleted, "Test setup assumption: claim should still be waiting.");

    ledger.Complete(null);

    var claimed = await claimTask.WaitAsync(TimeSpan.FromSeconds(5));

    Assert.AreEqual(10, claimed);
  }

  [TestMethod]
  public async Task ClaimAsync_CompleteWithLeftoverBelowMinimum_ClaimsNothing()
  {
    var ledger = new BudgetLedger(3);

    var claimTask = ledger.ClaimAsync(30, 5);
    ledger.TryResolvePendingBatch();

    await Task.Delay(20);

    ledger.Complete(null);

    var claimed = await claimTask.WaitAsync(TimeSpan.FromSeconds(5));

    Assert.AreEqual(0, claimed);
  }

  [TestMethod]
  public void Complete_WithActualAvailable_OverwritesAvailable_UpwardOrDownward()
  {
    // Both claims request far more than their ledger's initial seed, so neither can resolve via
    // the fast batch path — both are still genuinely pending when Complete() fires, which is the
    // only case Complete()'s reconciliation can actually affect (a claim already resolved via the
    // fast path is already handed out and tracked in _inFlight; reconciliation can't retroactively
    // change it).

    // Upward: the local total under-tracked reality.
    var ledgerUp = new BudgetLedger(0);
    var upClaim = ledgerUp.ClaimAsync(100, 1);
    ledgerUp.TryResolvePendingBatch();
    ledgerUp.Complete(100);

    Assert.AreEqual(100m, upClaim.GetAwaiter().GetResult());

    // Downward: the local total over-estimated reality.
    var ledgerDown = new BudgetLedger(50);
    var downClaim = ledgerDown.ClaimAsync(1000, 1);
    ledgerDown.TryResolvePendingBatch();
    ledgerDown.Complete(10);

    Assert.AreEqual(10m, downClaim.GetAwaiter().GetResult());
  }

  [TestMethod]
  public void Complete_SubtractsInFlightFromActualAvailable_SoAnUnconfirmedClaimIsNotDoubleCounted()
  {
    var ledger = new BudgetLedger(100);

    // Claim 60 (immediately, since it's affordable alone) but don't Settle it yet — simulates an
    // order that's been claimed for but whose placement hasn't round-tripped through the exchange
    // (and therefore isn't yet reflected as held in a freshly fetched balance) at the moment
    // reconciliation happens.
    var claimTask = ledger.ClaimAsync(60, 1);
    ledger.TryResolvePendingBatch();
    var claimed = claimTask.GetAwaiter().GetResult();
    Assert.AreEqual(60m, claimed);

    // A fresh fetch shows 100 still available (the in-flight 60 not yet reflected as held).
    // Naively overwriting with 100 would let a second claim take another 60 out of the same money.
    ledger.Complete(100);

    // Nothing should be claimable beyond the (100 - 60 in-flight) = 40 that's genuinely free.
    var secondTask = ledger.ClaimAsync(50, 1); // ledger is already complete, resolves immediately
    var second = secondTask.GetAwaiter().GetResult();
    Assert.AreEqual(40m, second);
  }

  [TestMethod]
  public void Settle_ReturnsUnspentPortionToPool()
  {
    var ledger = new BudgetLedger(100);

    var claimTask = ledger.ClaimAsync(60, 1);
    ledger.TryResolvePendingBatch();
    var claimed = claimTask.GetAwaiter().GetResult();
    Assert.AreEqual(60m, claimed);

    // Only 40 was actually spent (e.g. a partial fill, or a rounding remainder) — the other 20
    // should come back.
    ledger.Settle(claimed, 40);

    var nextTask = ledger.ClaimAsync(60, 1);
    ledger.TryResolvePendingBatch();
    var next = nextTask.GetAwaiter().GetResult();
    Assert.AreEqual(60m, next); // (100 - 60) + 20 returned = 60 available again.
  }

  [TestMethod]
  public void Settle_DroppedClaim_ReturnsTheWholeClaimToPool()
  {
    var ledger = new BudgetLedger(100);

    var claimTask = ledger.ClaimAsync(60, 1);
    ledger.TryResolvePendingBatch();
    var claimed = claimTask.GetAwaiter().GetResult();

    // Nothing was spent at all (e.g. the order was dropped below the exchange minimum).
    ledger.Settle(claimed, 0);

    var nextTask = ledger.ClaimAsync(100, 1);
    ledger.TryResolvePendingBatch();
    var next = nextTask.GetAwaiter().GetResult();
    Assert.AreEqual(100m, next);
  }

  [TestMethod]
  public async Task ConcurrentClaims_NeverOverdraw_AndSumOfClaimsMatchesTotalAvailable()
  {
    // Stress test: many real concurrent claimants racing against a bounded pool, all registered
    // together (so the all-or-nothing batch check is meaningful) and resolved via a mix of
    // incremental deposits and a final Complete(). Verifies the ledger never hands out more than
    // what's actually deposited, regardless of interleaving.
    const int claimantCount = 50;
    const decimal perClaim = 10;
    const decimal initial = 0;
    const decimal perDeposit = 10;

    var ledger = new BudgetLedger(initial);

    var claimTasks = Enumerable.Range(0, claimantCount)
      .Select(_ => ledger.ClaimAsync(perClaim, 1))
      .ToArray();

    ledger.TryResolvePendingBatch();

    var depositTasks = Enumerable.Range(0, claimantCount)
      .Select(async _ =>
      {
        await Task.Delay(Random.Shared.Next(0, 10));
        ledger.Deposit(perDeposit);
      })
      .ToArray();

    await Task.WhenAll(depositTasks);

    ledger.Complete(null);

    var claimed = await Task.WhenAll(claimTasks);

    var totalClaimed = claimed.Sum();
    var totalAvailable = initial + claimantCount * perDeposit;

    Assert.IsTrue(totalClaimed <= totalAvailable,
      $"Claimed {totalClaimed} but only {totalAvailable} was ever available.");
    Assert.AreEqual(totalAvailable, totalClaimed); // Every deposited unit was claimable by someone.
  }

  /// <summary>
  /// Regresses a real bug caught during review, not by a failing test, in
  /// <c>RebalancingService.ClaimAndPlaceBuy</c>'s buy-side dust-remainder top-up (see CLAUDE.md's
  /// "Bitvavo integration: known future improvements" entry on this fix). A leg that makes TWO
  /// independent <see cref="BudgetLedger.ClaimAsync"/> calls (its own original claim, then a
  /// separate top-up claim for a dust remainder) must settle them as a SINGLE combined
  /// <see cref="BudgetLedger.Settle"/> call — using the SUM of both claimed amounts — or the
  /// second claim's own contribution to <c>_inFlight</c> is never released. That leftover residue
  /// is invisible in <c>_available</c> immediately (both claims already fully resolved by the time
  /// they're settled here), but corrupts the ONE authoritative calculation
  /// <see cref="BudgetLedger.Complete"/> ever makes (<c>_available = actual - _inFlight</c>) for
  /// whatever ELSE is still pending at that point — silently shortchanging a completely unrelated
  /// leg, even though the true account balance had more than enough for it.
  /// </summary>
  [TestMethod]
  public async Task ClaimTwiceThenSettleAsOneCombinedPair_DoesNotLeaveInFlightResidue_ForALaterPendingClaim()
  {
    var ledger = new BudgetLedger(0);

    // Leg A's original claim, granted immediately via an exact-covering deposit (mirrors a sell
    // leg's proceeds landing well before final reconciliation).
    var originalClaimTask = ledger.ClaimAsync(requested: 20, minClaimable: 1);
    ledger.Deposit(20);
    var claimedOriginal = await originalClaimTask.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.AreEqual(20, claimedOriginal);

    // Leg A's separate dust top-up claim, ALSO granted early via its own exact-covering deposit —
    // this is the second, independent ClaimAsync call that must not be settled separately.
    var topUpClaimTask = ledger.ClaimAsync(requested: 0.10m, minClaimable: 0.10m);
    ledger.Deposit(0.10m);
    var claimedTopUp = await topUpClaimTask.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.AreEqual(0.10m, claimedTopUp);

    // Leg A fully spends everything it claimed (both the original amount and the top-up), the
    // correct fix, per ClaimAndPlaceBuy's own remarks: ONE Settle call, claimed = sum of both.
    ledger.Settle(claimedOriginal + claimedTopUp, spent: 20.10m);

    // An entirely unrelated leg, still pending when final reconciliation runs.
    var otherLegTask = ledger.ClaimAsync(requested: 5, minClaimable: 1);

    // The true account balance has exactly enough for this other leg — nothing left over from Leg
    // A (it spent every cent it claimed), nothing missing either.
    ledger.Complete(actualAvailable: 5);

    var claimedOtherLeg = await otherLegTask.WaitAsync(TimeSpan.FromSeconds(5));

    // Under the reverted-and-fixed bug (settling only the original claim, with the top-up's cost
    // subtracted from `spent` instead of added to `claimed`), Leg A's Settle call would leave 0.10
    // permanently stuck in _inFlight, and Complete would compute _available as 5 - 0.10 = 4.90 —
    // short of the 5 requested, so this claim would fall into the proportional-split branch and
    // receive only 4.90 instead of its full, genuinely-available 5.
    Assert.AreEqual(5, claimedOtherLeg);
  }
}