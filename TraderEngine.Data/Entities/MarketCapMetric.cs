namespace TraderEngine.Data.Entities;

/// <summary>
/// A single market-cap observation for one asset at one point in time. Backed by a TimescaleDB
/// hypertable partitioned on <see cref="Updated"/> (see the migration that creates this table).
/// </summary>
public class MarketCapMetric
{
  public string QuoteSymbol { get; set; } = null!;

  public string BaseSymbol { get; set; } = null!;

  public DateTime Updated { get; set; }

  public double Price { get; set; }

  public double MarketCap { get; set; }

  public List<string> Tags { get; set; } = [];
}
