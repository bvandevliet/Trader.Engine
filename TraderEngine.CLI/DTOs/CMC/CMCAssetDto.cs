namespace TraderEngine.CLI.DTOs.CMC;

internal class CMCAssetDto
{
  /// <summary>
  /// The name of this cryptocurrency.
  /// </summary>
  public string Name { get; set; } = string.Empty;

  /// <summary>
  /// The ticker symbol for this cryptocurrency.
  /// </summary>
  public string Symbol { get; set; } = string.Empty;

  /// <summary>
  /// The web URL friendly shorthand version of this cryptocurrency name.
  /// </summary>
  public string Slug { get; set; } = string.Empty;

  /// <summary>
  /// The cryptocurrency's CoinMarketCap rank by market cap.
  /// </summary>
  public int Cmc_Rank { get; set; }

  /// <summary>
  /// The approximate number of coins circulating for this cryptocurrency.
  /// </summary>
  //public ulong? Circulating_Supply { get; set; }

  /// <summary>
  /// The approximate total amount of coins in existence right now (minus any coins that have been verifiably burned).
  /// </summary>
  //public ulong? Total_Supply { get; set; }

  /// <summary>
  /// The expected maximum limit of coins ever to be available for this cryptocurrency.
  /// </summary>
  //public ulong? Max_Supply { get; set; }

  /// <summary>
  /// Array of tags associated with this cryptocurrency.
  /// </summary>
  public string[] Tags { get; set; } = Array.Empty<string>();

  /// <summary>
  /// Timestamp (ISO 8601) of the last time this cryptocurrency's market data was updated.
  /// </summary>
  public DateTime Last_Updated { get; set; }

  /// <summary>
  /// A map of market quotes in different currency conversions.
  /// </summary>
  public Dictionary<string, CMCQuoteDto> Quote { get; set; } = new Dictionary<string, CMCQuoteDto>();
}