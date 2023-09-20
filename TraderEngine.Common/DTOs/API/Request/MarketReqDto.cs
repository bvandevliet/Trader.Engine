using System.ComponentModel.DataAnnotations;

namespace TraderEngine.Common.DTOs.API.Request;

/// <summary>
/// Market, pair of quote currency and base currency.
/// </summary>
public class MarketReqDto : IEquatable<MarketReqDto>
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

  public MarketReqDto()
  {
  }

  /// <param name="quoteSymbol"><inheritdoc cref="QuoteSymbol"/></param>
  /// <param name="baseSymbol"><inheritdoc cref="BaseSymbol"/></param>
  public MarketReqDto(string quoteSymbol, string baseSymbol)
  {
    QuoteSymbol = quoteSymbol;
    BaseSymbol = baseSymbol;
  }

  public override bool Equals(object? obj) =>
    Equals(obj as MarketReqDto);

  public bool Equals(MarketReqDto? obj) =>
    obj is not null
      && QuoteSymbol == obj.QuoteSymbol
      && BaseSymbol == obj.BaseSymbol;

  public override int GetHashCode() =>
    $"{QuoteSymbol}{BaseSymbol}".GetHashCode();

  public static bool operator ==(MarketReqDto a, MarketReqDto b) => a.Equals(b);
  public static bool operator !=(MarketReqDto a, MarketReqDto b) => !(a == b);
}