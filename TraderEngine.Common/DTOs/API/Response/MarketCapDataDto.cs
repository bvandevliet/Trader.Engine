using TraderEngine.Common.DTOs.API.Request;

namespace TraderEngine.Common.DTOs.API.Response;

public class MarketCapDataDto
{
  /// <summary>
  /// Market in which the market cap is calculated.
  /// </summary>
  public MarketReqDto Market { get; set; } = null!;

  /// <summary>
  /// The asset's display name as reported by CoinMarketCap (e.g. "Bitcoin" for BTC).
  /// </summary>
  public string Name { get; set; } = string.Empty;

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