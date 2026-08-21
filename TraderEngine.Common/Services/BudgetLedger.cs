namespace TraderEngine.Common.Services;

/// <summary>
/// Coordinates concurrent sell and buy legs of a single rebalance run so a buy leg can claim its
/// share of proceeds as soon as enough has actually landed, rather than every buy waiting for
/// every sell in the batch to finish (including the slowest one).
///
/// <para>
/// Deposits are driven by each sell leg's own settled proceeds (e.g. an exchange-reported
/// <c>AmountQuoteFilled</c> minus <c>FeePaid</c>) as it resolves — authoritative for that leg,
/// not an estimate. <see cref="Complete"/> additionally performs a one-time reconciliation against
/// a freshly fetched, authoritative balance figure once every sell is known to have resolved, as a
/// safety net against anything the locally-summed total can't model (other account activity, a
/// missed event, an unmodeled deduction).
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

  private TaskCompletionSource _changeSignal = NewSignal();

  public BudgetLedger(decimal initialAvailable)
  {
    _available = initialAvailable;
  }

  private static TaskCompletionSource NewSignal()
  {
    return new(TaskCreationOptions.RunContinuationsAsynchronously);
  }

  /// <summary>
  /// Adds a sell leg's realized net proceeds to the pool, waking any claims currently waiting.
  /// </summary>
  public void Deposit(decimal amount)
  {
    if (amount == 0)
      return;

    lock (_gate)
    {
      _available += amount;
      Broadcast();
    }
  }

  /// <summary>
  /// Waits until <paramref name="requested"/> is fully available, or — once <see cref="Complete"/>
  /// has been called, meaning no further deposits will ever arrive — claims whatever's left if it
  /// clears <paramref name="minClaimable"/>, else claims nothing. A non-zero claim must be reported
  /// back via <see cref="Settle"/> once its outcome (how much, if any, was actually spent) is known.
  /// </summary>
  public async Task<decimal> ClaimAsync(decimal requested, decimal minClaimable)
  {
    while (true)
    {
      Task waitTask;

      lock (_gate)
      {
        if (requested <= 0)
          return 0;

        if (_available >= requested)
        {
          _available -= requested;
          _inFlight += requested;
          return requested;
        }

        if (_isComplete)
        {
          var claimed = _available >= minClaimable ? _available : 0;

          if (claimed > 0)
          {
            _available -= claimed;
            _inFlight += claimed;
          }

          return claimed;
        }

        waitTask = _changeSignal.Task;
      }

      await waitTask;
    }
  }

  /// <summary>
  /// Reports the outcome of a claim previously granted by <see cref="ClaimAsync"/>: <paramref name="claimed"/>
  /// is exactly what that call returned, <paramref name="spent"/> is what was actually placed for
  /// (e.g. an order's own realized cost — 0 if the order was dropped or failed outright). Any
  /// unspent difference is returned to the pool for other legs to claim.
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
        Broadcast();
      }
    }
  }

  /// <summary>
  /// Marks the ledger final: no more deposits are coming, so any still-waiting claim should stop
  /// waiting and take whatever's left. <paramref name="actualAvailable"/>, if given, is a freshly
  /// fetched, authoritative balance figure; <see cref="_inFlight"/> is subtracted from it first,
  /// since that money is already locally committed to a still-in-progress claim the exchange may
  /// not yet reflect as held — applying the correction without that adjustment risks handing the
  /// same funds out a second time. This can move <see cref="_available"/> either up or down
  /// relative to the locally-tracked total, catching whatever the local deposit-sum couldn't model.
  /// </summary>
  public void Complete(decimal? actualAvailable)
  {
    lock (_gate)
    {
      if (actualAvailable is { } actual)
        _available = Math.Max(0, actual - _inFlight);

      _isComplete = true;
      Broadcast();
    }
  }

  private void Broadcast()
  {
    var signal = _changeSignal;
    _changeSignal = NewSignal();
    signal.TrySetResult();
  }
}
