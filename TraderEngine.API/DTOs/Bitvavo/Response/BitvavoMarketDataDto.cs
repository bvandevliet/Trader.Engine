namespace TraderEngine.API.DTOs.Bitvavo.Response;

public class BitvavoMarketDataDto
{
  /// <summary>
  /// e.g. "BTC-EUR". Absent from a single-market <c>GET /markets?market=X</c> response (the
  /// caller already knows which market it asked for), but present on each entry of the
  /// unfiltered, all-markets <c>GET /markets</c> response used to key <see cref="BitvavoExchange.GetMarkets"/>'s result.
  /// </summary>
  public string? Market { get; set; }

  /// <summary>
  /// Enum: "trading" "halted" "auction".
  /// </summary>
  public string Status { get; set; } = null!;

  /// <summary>
  /// Determines how many significant digits are allowed.
  /// The rationale behind this is that for higher amounts, smaller price increments are less relevant.
  /// </summary>
  public int? PricePrecision { get; set; }

  /// <summary>
  /// The minimum amount in quote currency for valid orders.
  /// </summary>
  public decimal MinOrderInQuoteAsset { get; set; }

  /// <summary>
  /// The minimum amount in base currency for valid orders.
  /// </summary>
  public decimal MinOrderInBaseAsset { get; set; }
}