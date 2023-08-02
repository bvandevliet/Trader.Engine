namespace TraderEngine.Common.DTOs.Database;

public class MarketCapDataDb
{
  public string QuoteSymbol { get; set; } = null!;

  public string BaseSymbol { get; set; } = null!;

  public double Price { get; set; }

  public double MarketCap { get; set; }

  public List<string> Tags { get; set; } = new();

  public DateTime Updated { get; set; }
}