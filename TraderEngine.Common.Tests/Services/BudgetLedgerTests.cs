using TraderEngine.Common.Services;

namespace TraderEngine.Common.Tests.Services;

/// <summary>
/// Covers <see cref="BudgetLedger"/> in isolation: the async wait/broadcast mechanics, the
/// in-flight-aware reconciliation in <see cref="BudgetLedger.Complete"/>, and concurrent-claim
/// safety under real parallelism (not just the cooperative single-threaded interleaving the
/// higher-level <c>RebalancingService</c> tests exercise via synchronous test doubles).
/// </summary>
[TestClass]
public class BudgetLedgerTests
{
  [TestMethod]
  public async Task ClaimAsync_EnoughAvailable_ClaimsImmediately_NoWaiting()
  {
    var ledger = new BudgetLedger(100);

    var claimed = await ledger.ClaimAsync(40, 1);

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
  public async Task ClaimAsync_InsufficientUntilDeposit_WaitsThenClaimsOnceEnoughArrives()
  {
    var ledger = new BudgetLedger(10);

    var claimTask = ledger.ClaimAsync(30, 1);

    // Give the claim a chance to actually start waiting before depositing.
    await Task.Delay(20);
    Assert.IsFalse(claimTask.IsCompleted, "Test setup assumption: claim should still be waiting.");

    ledger.Deposit(25);

    var claimed = await claimTask.WaitAsync(TimeSpan.FromSeconds(5));

    Assert.AreEqual(30, claimed);
  }

  [TestMethod]
  public async Task ClaimAsync_CompleteWithNoDeposits_ClaimsWhateverIsLeftIfAboveMinimum()
  {
    var ledger = new BudgetLedger(10);

    var claimTask = ledger.ClaimAsync(30, 5);

    await Task.Delay(20);

    ledger.Complete(null);

    var claimed = await claimTask.WaitAsync(TimeSpan.FromSeconds(5));

    Assert.AreEqual(10, claimed);
  }

  [TestMethod]
  public async Task ClaimAsync_CompleteWithLeftoverBelowMinimum_ClaimsNothing()
  {
    var ledger = new BudgetLedger(3);

    var claimTask = ledger.ClaimAsync(30, 5);

    await Task.Delay(20);

    ledger.Complete(null);

    var claimed = await claimTask.WaitAsync(TimeSpan.FromSeconds(5));

    Assert.AreEqual(0, claimed);
  }

  [TestMethod]
  public void Complete_WithActualAvailable_OverwritesAvailable_UpwardOrDownward()
  {
    // Upward: the local total under-tracked reality (e.g. a deposit whose proceeds weren't fully
    // captured) — reconciliation should still be able to raise the pool, not just lower it.
    var ledgerUp = new BudgetLedger(0);
    ledgerUp.Complete(100);

    Assert.AreEqual(100m, ledgerUp.ClaimAsync(100, 1).GetAwaiter().GetResult());

    // Downward: the local total over-estimated reality.
    var ledgerDown = new BudgetLedger(100);
    ledgerDown.Complete(10);

    Assert.AreEqual(10m, ledgerDown.ClaimAsync(100, 1).GetAwaiter().GetResult());
  }

  [TestMethod]
  public void Complete_SubtractsInFlightFromActualAvailable_SoAnUnconfirmedClaimIsNotDoubleCounted()
  {
    var ledger = new BudgetLedger(100);

    // Claim 60 but don't Settle it yet — simulates an order that's been claimed for but whose
    // placement hasn't round-tripped through the exchange (and therefore isn't yet reflected as
    // held in a freshly fetched balance) at the moment reconciliation happens.
    var claimed = ledger.ClaimAsync(60, 1).GetAwaiter().GetResult();
    Assert.AreEqual(60m, claimed);

    // A fresh fetch shows 100 still available (the in-flight 60 not yet reflected as held).
    // Naively overwriting with 100 would let a second claim take another 60 out of the same money.
    ledger.Complete(100);

    // Nothing should be claimable beyond the (100 - 60 in-flight) = 40 that's genuinely free.
    var second = ledger.ClaimAsync(50, 1).GetAwaiter().GetResult();
    Assert.AreEqual(40m, second);
  }

  [TestMethod]
  public void Settle_ReturnsUnspentPortionToPool()
  {
    var ledger = new BudgetLedger(100);

    var claimed = ledger.ClaimAsync(60, 1).GetAwaiter().GetResult();
    Assert.AreEqual(60m, claimed);

    // Only 40 was actually spent (e.g. a partial fill, or a rounding remainder) — the other 20
    // should come back.
    ledger.Settle(claimed, 40);

    var next = ledger.ClaimAsync(60, 1).GetAwaiter().GetResult();
    Assert.AreEqual(60m, next); // (100 - 60) + 20 returned = 60 available again.
  }

  [TestMethod]
  public void Settle_DroppedClaim_ReturnsTheWholeClaimToPool()
  {
    var ledger = new BudgetLedger(100);

    var claimed = ledger.ClaimAsync(60, 1).GetAwaiter().GetResult();

    // Nothing was spent at all (e.g. the order was dropped below the exchange minimum).
    ledger.Settle(claimed, 0);

    var next = ledger.ClaimAsync(100, 1).GetAwaiter().GetResult();
    Assert.AreEqual(100m, next);
  }

  [TestMethod]
  public async Task ConcurrentClaims_NeverOverdraw_AndSumOfClaimsMatchesTotalAvailable()
  {
    // Stress test: many real concurrent claimants racing against a bounded pool, some of which
    // must be satisfied via waiting on deposits arriving from other concurrent tasks. Verifies
    // the ledger never hands out more than what's actually deposited, regardless of interleaving.
    const int claimantCount = 50;
    const decimal perClaim = 10;
    const decimal initial = 0;
    const decimal perDeposit = 10;

    var ledger = new BudgetLedger(initial);

    var claimTasks = Enumerable.Range(0, claimantCount)
      .Select(_ => ledger.ClaimAsync(perClaim, 1))
      .ToArray();

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

    Assert.IsTrue(totalClaimed <= totalAvailable, $"Claimed {totalClaimed} but only {totalAvailable} was ever available.");
    Assert.AreEqual(totalAvailable, totalClaimed); // Every deposited unit was claimable by someone.
  }
}
