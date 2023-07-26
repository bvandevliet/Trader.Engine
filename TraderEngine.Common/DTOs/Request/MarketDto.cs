using System.ComponentModel.DataAnnotations;

namespace TraderEngine.Common.DTOs.Request;

/// <summary>
/// Market, pair of quote currency and base currency.
/// </summary>
public class MarketDto : IEquatable<MarketDto>
{
  /// <summary>
  /// The quote currency to value <see cref="BaseSymbol"/> against.
  /// </summary>
  [Required]
  public string QuoteSymbol { get; set; } = null!;

  /// <summary>
  /// The base currency valued by <see cref="QuoteSymbol"/>.
  /// </summary>
  [Required]
  public string BaseSymbol { get; set; } = null!;

  public MarketDto()
  {
  }

  /// <param name="quoteSymbol"><inheritdoc cref="QuoteSymbol"/></param>
  /// <param name="baseSymbol"><inheritdoc cref="BaseSymbol"/></param>
  public MarketDto(string quoteSymbol, string baseSymbol)
  {
    QuoteSymbol = quoteSymbol;
    BaseSymbol = baseSymbol;
  }

  public override bool Equals(object? obj) =>
    Equals(obj as MarketDto);

  public bool Equals(MarketDto? obj) =>
    obj is not null
      && QuoteSymbol == obj.QuoteSymbol
      && BaseSymbol == obj.BaseSymbol;

  public override int GetHashCode() =>
    $"{QuoteSymbol}{BaseSymbol}".GetHashCode();

  public static bool operator ==(MarketDto a, MarketDto b) => a.Equals(b);
  public static bool operator !=(MarketDto a, MarketDto b) => !(a == b);
}