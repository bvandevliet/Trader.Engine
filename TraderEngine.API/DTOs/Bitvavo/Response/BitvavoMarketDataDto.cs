namespace TraderEngine.API.DTOs.Bitvavo.Response;

public class BitvavoMarketDataDto
{
  /// <summary>
  /// Enum: "trading" "halted" "auction".
  /// </summary>
  public string Status { get; set; } = null!;

  /// <summary>
  /// Determines how many significant digits are allowed.
  /// The rationale behind this is that for higher amounts, smaller price increments are less relevant.
  /// </summary>
  public int PricePrecision { get; set; }

  /// <summary>
  /// The minimum amount in quote currency for valid orders.
  /// </summary>
  public decimal MinOrderInQuoteAsset { get; set; }

  /// <summary>
  /// The minimum amount in base currency for valid orders.
  /// </summary>
  public decimal MinOrderInBaseAsset { get; set; }
}