namespace TraderEngine.API.DTOs.Bitvavo.Response;

public class BitvavoMarketDataDto
{
  public string Market { get; set; } = null!;

  /// <summary>
  /// Enum: "trading" "halted" "auction".
  /// </summary>
  public string Status { get; set; } = null!;

  /// <summary>
  /// Base currency, found on the left side of the dash in market.
  /// </summary>
  public string Base { get; set; } = null!;

  /// <summary>
  /// Quote currency, found on the right side of the dash in market.
  /// </summary>
  public string Quote { get; set; } = null!;

  /// <summary>
  /// Determines how many significant digits are allowed.
  /// The rationale behind this is that for higher amounts, smaller price increments are less relevant.
  /// </summary>
  public string PricePrecision { get; set; } = null!;

  /// <summary>
  /// The minimum amount in quote currency for valid orders.
  /// </summary>
  public string MinOrderInQuoteCurrency { get; set; } = null!;

  /// <summary>
  /// The minimum amount in base currency for valid orders.
  /// </summary>
  public string MinOrderInBaseCurrency { get; set; } = null!;
}