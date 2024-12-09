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
    QuoteSymbol = quoteSymbol.ToUpper();
    BaseSymbol = baseSymbol.ToUpper();
  }

  public override string ToString() => $"{BaseSymbol.ToUpper()}-{QuoteSymbol.ToUpper()}";

  public override int GetHashCode() => ToString().GetHashCode();

  public bool Equals(MarketReqDto? obj) => obj is not null
    && QuoteSymbol.Equals(obj.QuoteSymbol, StringComparison.OrdinalIgnoreCase)
    && BaseSymbol.Equals(obj.BaseSymbol, StringComparison.OrdinalIgnoreCase);

  public override bool Equals(object? obj) => Equals(obj as MarketReqDto);

  public static bool operator ==(MarketReqDto? a, MarketReqDto? b) => a?.Equals(b) is true;
  public static bool operator !=(MarketReqDto? a, MarketReqDto? b) => a?.Equals(b) is not true;
}