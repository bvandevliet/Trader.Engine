using System.Collections.ObjectModel;
using TraderEngine.Common.Exceptions;

namespace TraderEngine.Common.Models;

/// <summary>
/// Represents a portfolio balance, containing relative asset allocations.
/// </summary>
public class Balance
{
  /// <summary>
  /// Triggered when <see cref="AmountQuoteTotal"/> has changed.
  /// </summary>
  public event EventHandler? OnAmountQuoteTotalReset;

  /// <summary>
  /// Triggered when <see cref="AmountQuote"/> has changed.
  /// </summary>
  public event EventHandler? OnAmountQuoteAvailableReset;

  /// <summary>
  /// The quote currency on which this balance instance is based.
  /// </summary>
  public string QuoteSymbol { get; }

  private readonly List<Allocation> _allocations = new();
  /// <summary>
  /// Collection of <see cref="Allocation"/> instances.
  /// </summary>
  public ReadOnlyCollection<Allocation> Allocations { get; }

  private decimal? _amountQuote;
  /// <summary>
  /// Amount of quote currency.
  /// </summary>
  public decimal AmountQuote
  {
    get => _amountQuote ??= GetAllocation(QuoteSymbol)?.AmountQuote ?? 0;
  }

  private decimal? _amountQuoteTotal;
  /// <summary>
  /// Total value of balance in quote currency.
  /// </summary>
  public decimal AmountQuoteTotal
  {
    get => _amountQuoteTotal ??= Allocations.Sum(alloc => alloc.AmountQuote);
  }

  /// <summary>
  /// Collection of <see cref="Allocation"/> instances and total quote amount values.
  /// </summary>
  /// <param name="quoteCurrency"><inheritdoc cref="QuoteSymbol"/></param>
  public Balance(string quoteCurrency)
  {
    QuoteSymbol = quoteCurrency;

    Allocations = _allocations.AsReadOnly();
  }

  /// <summary>
  /// Get an <see cref="Allocation"/> for the given <paramref name="baseSymbol"/> if exists.
  /// </summary>
  /// <param name="baseSymbol">The asset to find allocation of.</param>
  /// <returns></returns>
  public Allocation? GetAllocation(string baseSymbol) =>
    _allocations.Find(alloc => alloc.Market.BaseSymbol.Equals(baseSymbol));

  /// <summary>
  /// Add <paramref name="allocation"/> to the <see cref="Allocations"/> collection.
  /// Note that <see cref="AmountQuoteTotal"/> will be reset and related events will be triggered.
  /// </summary>
  /// <param name="allocation">The <see cref="Allocation"/> to add.</param>
  /// <exception cref="InvalidObjectException"></exception>
  /// <exception cref="ObjectAlreadyExistsException"></exception>
  public void AddAllocation(Allocation allocation)
  {
    if (QuoteSymbol != allocation.Market.QuoteSymbol)
    {
      throw new InvalidObjectException("Quote currency of given Allocation object does not match with the quote currency of this Balance instance.");
    }

    if (_allocations.Any(alloc => alloc.Market.Equals(allocation.Market)))
    {
      throw new ObjectAlreadyExistsException("An allocation in this market already exists.");
    }

    allocation.PriceUpdated += ResetAmountQuoteTotal;

    allocation.AmountUpdated += ResetAmountQuoteTotal;
    allocation.AmountQuoteUpdated += ResetAmountQuoteTotal;

    if (allocation.Market.BaseSymbol.Equals(QuoteSymbol))
    {
      allocation.AmountUpdated += ResetAmountQuoteAvailable;
      allocation.AmountQuoteUpdated += ResetAmountQuoteAvailable;
    }

    _allocations.Add(allocation);

    ResetAmountQuoteTotal();

    if (allocation.Market.BaseSymbol.Equals(QuoteSymbol))
    {
      ResetAmountQuoteAvailable();
    }
  }

  /// <summary>
  /// Remove an <see cref="Allocation"/> from the <see cref="Allocations"/> collection.
  /// Note that <see cref="AmountQuoteTotal"/> will be reset and related events will be triggered.
  /// </summary>
  /// <param name="baseSymbol">The asset to remove allocation of.</param>
  /// <returns>The <see cref="Allocation"/> that was removed.</returns>
  public Allocation? RemoveAllocation(string baseSymbol)
  {
    Allocation? allocation = GetAllocation(baseSymbol);

    if (allocation != null)
    {
      allocation.PriceUpdated -= ResetAmountQuoteTotal;

      allocation.AmountUpdated -= ResetAmountQuoteTotal;
      allocation.AmountQuoteUpdated -= ResetAmountQuoteTotal;

      allocation.AmountUpdated -= ResetAmountQuoteAvailable;
      allocation.AmountQuoteUpdated -= ResetAmountQuoteAvailable;

      _allocations.Remove(allocation);

      ResetAmountQuoteTotal();

      if (allocation.Market.BaseSymbol.Equals(QuoteSymbol))
      {
        ResetAmountQuoteAvailable();
      }
    }

    return allocation;
  }

  private void ResetAmountQuoteTotal(decimal? oldValue = null, decimal? newValue = null)
  {
    _amountQuoteTotal = null;

    OnAmountQuoteTotalReset?.Invoke(this, new());
  }

  private void ResetAmountQuoteAvailable(decimal? oldValue = null, decimal? newValue = null)
  {
    _amountQuote = null;

    OnAmountQuoteAvailableReset?.Invoke(this, new());
  }
}