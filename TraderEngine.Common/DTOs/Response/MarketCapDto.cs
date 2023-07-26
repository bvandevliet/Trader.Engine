namespace TraderEngine.Common.DTOs.Response;

public class MarketCapDto
{
  /// <summary>
  /// Timestamp of the last time this asset's market data was updated.
  /// </summary>
  public DateTime Updated { get; set; }

  /// <summary>
  /// Market in which the market cap is calculated.
  /// </summary>
  public Request.MarketDto Market { get; set; } = null!;

  public double Price { get; set; }

  public double MarketCap { get; set; }
}