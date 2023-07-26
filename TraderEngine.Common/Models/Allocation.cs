using TraderEngine.Common.DTOs.Request;

namespace TraderEngine.Common.Models;

/// <summary>
/// Represents an asset allocation.
/// </summary>
public class Allocation
{
  /// <summary>
  /// Raised when <see cref="Price"/> has changed.
  /// Note that <see cref="AmountQuote"/> will also become outdated.
  /// </summary>
  public event PriceUpdatedEvent? PriceUpdated;
  public delegate void PriceUpdatedEvent(decimal? oldValue, decimal? newValue);

  /// <summary>
  /// Triggered when <see cref="Amount"/> has changed.
  /// Note that <see cref="AmountQuote"/> will also become outdated.
  /// </summary>
  public event AmountUpdateEvent? AmountUpdated;
  public delegate void AmountUpdateEvent(decimal? oldValue, decimal? newValue);

  /// <summary>
  /// Triggered when <see cref="AmountQuote"/> has changed.
  /// Note that <see cref="Amount"/> will also become outdated.
  /// </summary>
  public event AmountQuoteUpdatedEvent? AmountQuoteUpdated;
  public delegate void AmountQuoteUpdatedEvent(decimal? oldValue, decimal? newValue);

  /// <summary>
  /// The market for this allocation.
  /// </summary>
  public MarketDto Market { get; }

  private decimal _price;
  /// <summary>
  /// Price in quote currency per unit of base currency.
  /// </summary>
  public decimal Price
  {
    get => _price;
    set
    {
      UpdatePrice(value);
    }
  }

  private decimal _amount;
  /// <summary>
  /// Amount in base currency.
  /// </summary>
  public decimal Amount
  {
    get => _amount;
    set
    {
      UpdateAmount(value);
    }
  }

  private decimal? _amountQuote;
  /// <summary>
  /// Amount in quote currency.
  /// </summary>
  public decimal AmountQuote
  {
    get => _amountQuote ??= Price * Amount;
    set
    {
      UpdateAmountQuote(value);
    }
  }

  public Allocation(
    MarketDto market,
    decimal? price = null,
    decimal? amount = null)
  {
    Market = market;
    _price = price ?? 0;
    _amount = amount ?? 0;
  }

  public Allocation(
    string quoteSymbol,
    string baseSymbol,
    decimal? price = null,
    decimal? amount = null)
    : this(new MarketDto(quoteSymbol, baseSymbol), price, amount)
  {
  }

  private void UpdatePrice(decimal newValue)
  {
    decimal oldValue = Price;
    _price = newValue;

    if (oldValue != newValue)
    {
      _amountQuote = null;

      PriceUpdated?.Invoke(oldValue, newValue);
    }
  }

  private void UpdateAmount(decimal newValue)
  {
    decimal oldValue = Amount;
    _amount = newValue;

    if (oldValue != newValue)
    {
      _amountQuote = null;

      AmountUpdated?.Invoke(oldValue, newValue);
    }
  }

  private void UpdateAmountQuote(decimal newValue)
  {
    decimal oldValue = AmountQuote;
    _amountQuote = newValue;

    if (oldValue != newValue)
    {
      //price *= oldValue == 0 ? 1 : newValue / oldValue;

      _amount = _price == 0 ? 0 : newValue / _price;

      AmountQuoteUpdated?.Invoke(oldValue, newValue);
    }
  }
}