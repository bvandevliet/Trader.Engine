using System.ComponentModel.DataAnnotations;

namespace TraderEngine.Common.DTOs.API.Request;

/// <summary>
/// Market, pair of quote currency and base currency.
/// </summary>
public class MarketReqDto : IEquatable<MarketReqDto>
{
  private string _quoteSymbol = null!;
  private string _baseSymbol = null!;

  /// <summary>
  /// The quote currency to value <see cref="BaseSymbol"/> against.
  /// </summary>
  [Required]
  public string QuoteSymbol { get => _quoteSymbol.ToUpper(); set => _quoteSymbol = value.ToUpper(); }

  /// <summary>
  /// The base currency valued by <see cref="QuoteSymbol"/>.
  /// </summary>
  [Required]
  public string BaseSymbol { get => _baseSymbol.ToUpper(); set => _baseSymbol = value.ToUpper(); }

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

  public override string ToString()
  {
    return $"{BaseSymbol}-{QuoteSymbol}";
  }

  public override int GetHashCode()
  {
    return ToString().GetHashCode();
  }

  public bool Equals(MarketReqDto? obj)
  {
    return obj is not null
    && QuoteSymbol.Equals(obj.QuoteSymbol, StringComparison.OrdinalIgnoreCase)
    && BaseSymbol.Equals(obj.BaseSymbol, StringComparison.OrdinalIgnoreCase);
  }

  public override bool Equals(object? obj)
  {
    return Equals(obj as MarketReqDto);
  }

  public static bool operator ==(MarketReqDto? a, MarketReqDto? b)
  {
    return a?.Equals(b) is true;
  }

  public static bool operator !=(MarketReqDto? a, MarketReqDto? b)
  {
    return a?.Equals(b) is not true;
  }
}