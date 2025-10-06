using TraderEngine.Common.Enums;

namespace TraderEngine.Common.DTOs.API.Response;

public class MarketDataDto
{
  /// <summary>
  /// Status of the market.
  /// </summary>
  public MarketStatus Status { get; set; } = MarketStatus.Unavailable;

  /// <summary>
  /// Determines how many significant digits are allowed.
  /// The rationale behind this is that for higher amounts, smaller price increments are less relevant.
  /// </summary>
  public int? PricePrecision { get; set; }

  /// <summary>
  /// The minimum amount in quote currency for valid orders.
  /// </summary>
  public decimal MinOrderSizeInQuote { get; set; }

  /// <summary>
  /// The minimum amount in base currency for valid orders.
  /// </summary>
  public decimal MinOrderSizeInBase { get; set; }
}