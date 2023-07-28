using TraderEngine.Common.DTOs.Request;

namespace TraderEngine.Common.DTOs.Response;

public class MarketCapDataDto
{
  /// <summary>
  /// Market in which the market cap is calculated.
  /// </summary>
  public MarketReqDto Market { get; set; } = null!;

  public double Price { get; set; }

  public double MarketCap { get; set; }

  /// <summary>
  /// Array of tags associated with this asset.
  /// </summary>
  public List<string> Tags { get; set; } = new();

  /// <summary>
  /// Timestamp of the last time this asset's market data was updated.
  /// </summary>
  public DateTime Updated { get; set; }
}