namespace TraderEngine.Common.DTOs.Response;

public class MarketDto : Request.MarketDto
{
  /// <summary>
  /// Determines how many significant digits are allowed.
  /// The rationale behind this is that for higher amounts, smaller price increments are less relevant.
  /// </summary>
  public int PricePrecision { get; set; }

  /// <summary>
  /// The minimum amount in quote currency for valid orders.
  /// </summary>
  public decimal MinOrderInQuoteCurrency { get; set; }

  /// <summary>
  /// The minimum amount in base currency for valid orders.
  /// </summary>
  public decimal MinOrderInBaseCurrency { get; set; }
}