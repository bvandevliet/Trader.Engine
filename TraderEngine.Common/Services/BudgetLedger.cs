namespace TraderEngine.Common.Services;

/// <summary>
/// Coordinates concurrent sell and buy legs of a single rebalance run so a buy leg can claim its
/// share of proceeds as soon as enough has actually landed, rather than every buy waiting for
/// every sell in the batch to finish (including the slowest one).
///
/// <para>
/// Deposits are driven by each sell leg's own settled proceeds (e.g. an exchange-reported
/// <c>AmountQuoteFilled</c> minus <c>FeePaid</c>) as it resolves, authoritative for that leg, not
/// an estimate. <see cref="Complete"/> additionally performs a one-time reconciliation against a
/// freshly fetched, authoritative balance figure once every sell is known to have resolved, as a
/// safety net against anything the locally-summed total can't model (other account activity, a
/// missed event, an unmodeled deduction).
/// </para>
///
/// <para>
/// Callers always request their own full, unscaled target via <see cref="ClaimAsync"/>; there is
/// deliberately no "estimate a fair ratio upfront and pre-scale every request by it" step. Every
/// claim is registered as pending and resolved either (a) as soon as the pool covers every claim
/// currently pending, ALL AT ONCE, not one at a time, or (b) once <see cref="Complete"/> is called,
/// i.e. once the account's true final total is known, as a fair, proportional split of whatever's
/// genuinely left across whichever claims are still outstanding at that exact moment. The
/// all-at-once condition in (a) is deliberate: resolving whichever individual claims happen to fit
/// right now, one at a time, would let a small request "cut in line" ahead of a much larger one
/// simply because it's small, consuming funds a proportionally fair split would have partly
/// reserved for the larger claim too, not because of any real funding shortfall.
/// </para>
///
/// <para>
/// This shape is a deliberate correction of an earlier version, twice over. First, that version
/// scaled every request upfront by a ratio estimated from projected (not yet realized) sell
/// proceeds, which could undershoot reality enough to push a leg's scaled claim below the exchange
/// minimum and drop it outright, even when the account could have easily afforded its full target
/// once every sell had actually settled. The fix for that first version, in turn, resolved each
/// claim's fast path individually (whichever fits right now, on its own), which is what let a small
/// claim jump ahead of a larger one purely by size, as described above, breaking proportional
/// fairness under a genuine shortfall in a way the original ratio-based version never did.
/// </para>
/// </summary>
internal sealed class BudgetLedger
{
  private readonly object _gate = new();

  private decimal _available;

  /// <summary>
  /// Sum of every currently outstanding <see cref="ClaimAsync"/> grant not yet reported back via
  /// <see cref="Settle"/>. Subtracted from an authoritative balance figure in <see cref="Complete"/>
  /// so a fetch that doesn't yet reflect a still-in-flight claim as held by the exchange can't hand
  /// that same money out a second time to another waiter.
  /// </summary>
  private decimal _inFlight;

  private bool _isComplete;

  /// <summary>Claims that couldn't be satisfied immediately and are waiting on a future deposit or on <see cref="Complete"/>.</summary>
  private readonly List<PendingClaim> _pending = [];

  private sealed class PendingClaim
  {
    public required decimal Requested { get; init; }
    public required decimal MinClaimable { get; init; }
    public required TaskCompletionSource<decimal> Result { get; init; }
  }

  public BudgetLedger(decimal initialAvailable)
  {
    _available = initialAvailable;
  }

  /// <summary>
  /// Adds a sell leg's realized net proceeds to the pool, then resolves every currently pending
  /// claim if (and only if) that amount now covers all of them at once.
  /// </summary>
  public void Deposit(decimal amount)
  {
    if (amount == 0)
      return;

    lock (_gate)
    {
      _available += amount;
      ResolvePendingIfFullyCovered();
    }
  }

  /// <summary>
  /// Requests <paramref name="requested"/> (the caller's own full, unscaled target). Registered as
  /// pending, then immediately resolved, together with every other currently pending claim, if the
  /// pool covers all of them right now (see the type-level remarks for why this is all-or-nothing
  /// rather than per-claim). Otherwise waits until either a future deposit covers the whole pending
  /// set, or <see cref="Complete"/> fires and resolves it as a fair (never guessed) share of
  /// whatever's genuinely left, if that share clears <paramref name="minClaimable"/>, else zero. A
  /// non-zero claim must be reported back via <see cref="Settle"/> once its outcome (how much, if
  /// any, was actually spent) is known.
  /// </summary>
  public Task<decimal> ClaimAsync(decimal requested, decimal minClaimable)
  {
    lock (_gate)
    {
      if (requested <= 0)
        return Task.FromResult(0m);

      // A claim arriving after Complete() has already fired (shouldn't happen in the normal
      // ExecuteInterleaved flow, where every claim is registered before reconciliation starts, but
      // handled defensively): nothing more is ever coming, so resolve immediately against
      // whatever's left rather than joining a queue that will never be drained again.
      if (_isComplete)
      {
        var claimed = _available >= minClaimable ? Math.Min(_available, requested) : 0;

        if (claimed > 0)
        {
          _available -= claimed;
          _inFlight += claimed;
        }

        return Task.FromResult(claimed);
      }

      // Deliberately NOT auto-resolved here, even if `requested` alone already fits within
      // `_available` right now: a batch of claims is typically registered via a tight loop of
      // ClaimAsync calls (one per buy leg), and checking "is everyone currently pending covered"
      // after each individual registration would let whichever leg registers first resolve alone
      // — the exact "small claim cuts in line ahead of a larger one" problem this design exists to
      // avoid (see type-level remarks). The caller registers the whole batch first, then calls
      // <see cref="TryResolvePendingBatch"/> once explicitly; <see cref="Deposit"/>/<see cref="Settle"/>
      // also call it, since a later-arriving deposit is new information, not another member of the
      // same initial batch.
      var pending = new PendingClaim
      {
        Requested = requested,
        MinClaimable = minClaimable,
        Result = new TaskCompletionSource<decimal>(TaskCreationOptions.RunContinuationsAsynchronously),
      };

      _pending.Add(pending);

      return pending.Result.Task;
    }
  }

  /// <summary>
  /// Resolves every currently pending claim, all at once, if (and only if) <see cref="_available"/>
  /// covers the sum of all of them right now. Callers that register a batch of claims via
  /// <see cref="ClaimAsync"/> in a loop must call this once after the whole batch is registered
  /// (see the remarks on <see cref="ClaimAsync"/> for why it isn't done automatically per-claim).
  /// </summary>
  public void TryResolvePendingBatch()
  {
    lock (_gate)
    {
      ResolvePendingIfFullyCovered();
    }
  }

  /// <summary>
  /// Reports the outcome of a claim previously granted by <see cref="ClaimAsync"/>: <paramref name="claimed"/>
  /// is exactly what that call returned, <paramref name="spent"/> is what was actually placed for
  /// (e.g. an order's own realized cost, 0 if the order was dropped or failed outright). Any
  /// unspent difference is returned to the pool, resolving every currently pending claim if it now
  /// covers all of them at once.
  /// </summary>
  public void Settle(decimal claimed, decimal spent)
  {
    if (claimed == 0)
      return;

    lock (_gate)
    {
      _inFlight -= claimed;

      var unspent = claimed - spent;

      if (unspent != 0)
      {
        _available += unspent;
        ResolvePendingIfFullyCovered();
      }
    }
  }

  /// <summary>
  /// Marks the ledger final: no more deposits are coming. <paramref name="actualAvailable"/>, if
  /// given, is a freshly fetched, authoritative balance figure; <see cref="_inFlight"/> is
  /// subtracted from it first, since that money is already locally committed to a still-in-progress
  /// claim the exchange may not yet reflect as held, applying the correction without that
  /// adjustment risks handing the same funds out a second time. This can move <see cref="_available"/>
  /// either up or down relative to the locally-tracked total, catching whatever the local
  /// deposit-sum couldn't model.
  ///
  /// <para>
  /// Whatever's genuinely left is then split, in one pass, proportionally across every claim still
  /// pending at this exact moment, scaled by each one's own share of the total still outstanding.
  /// This is the only place a "fair share under a genuine shortfall" ratio is ever computed, and
  /// it's always computed from the true final numbers, never from an upfront guess.
  /// </para>
  /// </summary>
  public void Complete(decimal? actualAvailable)
  {
    lock (_gate)
    {
      if (actualAvailable is { } actual)
        _available = Math.Max(0, actual - _inFlight);

      _isComplete = true;

      if (_pending.Count == 0)
        return;

      var totalOutstanding = _pending.Sum(claim => claim.Requested);

      // Ample funds for everyone still pending: grant each its exact full request directly,
      // rather than via a computed ratio — a ratio of `_available / totalOutstanding` capped at 1
      // can still land fractionally short of an exact 1 due to plain decimal division/
      // multiplication rounding (e.g. 30 * (10 / 30) != exactly 10), which would needlessly shave
      // a fraction of a cent off an amount that should be exact.
      if (_available >= totalOutstanding)
      {
        foreach (var claim in _pending)
        {
          _available -= claim.Requested;
          _inFlight += claim.Requested;
          claim.Result.SetResult(claim.Requested);
        }
      }
      else
      {
        // Snapshot the pool before the loop: each claim's fair share is its proportion of the
        // TOTAL pool as it stood the moment Complete() was called, not of whatever's left after
        // earlier claims in this same pass have already been debited — mutating `_available`
        // in-place and reading it back for the next claim would skew every claim after the first
        // toward a smaller share than its true proportional entitlement.
        var poolAtCompletion = _available;

        foreach (var claim in _pending)
        {
          // Multiply before dividing, rather than pre-computing a `poolAtCompletion / totalOutstanding`
          // ratio and multiplying by that: when a claim's fair share should land on an exact
          // value (e.g. it's the only pending claim, so its share is exactly the whole pool),
          // dividing first can produce a non-terminating decimal that then multiplies back out to
          // something a hair short of exact (e.g. 30 * (10 / 30) != exactly 10). Computing the
          // product first avoids materializing that intermediate rounding error.
          var share = claim.Requested * poolAtCompletion / totalOutstanding;
          var claimed = share >= claim.MinClaimable ? share : 0;

          if (claimed > 0)
          {
            _available -= claimed;
            _inFlight += claimed;
          }

          claim.Result.SetResult(claimed);
        }
      }

      _pending.Clear();
    }
  }

  /// <summary>
  /// Resolves every currently pending claim, all at once, if (and only if) <see cref="_available"/>
  /// covers the sum of all of them right now. Must be called with <see cref="_gate"/> held.
  /// </summary>
  private void ResolvePendingIfFullyCovered()
  {
    if (_pending.Count == 0)
      return;

    var totalOutstanding = _pending.Sum(claim => claim.Requested);

    if (_available < totalOutstanding)
      return;

    foreach (var claim in _pending)
    {
      _available -= claim.Requested;
      _inFlight += claim.Requested;
      claim.Result.SetResult(claim.Requested);
    }

    _pending.Clear();
  }
}
