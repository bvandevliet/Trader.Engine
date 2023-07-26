namespace TraderEngine.CLI.DTOs;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>")]
internal class CMCAssetDto
{
  /// <summary>
  /// The name of this cryptocurrency.
  /// </summary>
  public string name { get; set; } = string.Empty;

  /// <summary>
  /// The ticker symbol for this cryptocurrency.
  /// </summary>
  public string symbol { get; set; } = string.Empty;

  /// <summary>
  /// The web URL friendly shorthand version of this cryptocurrency name.
  /// </summary>
  public string slug { get; set; } = string.Empty;

  /// <summary>
  /// The cryptocurrency's CoinMarketCap rank by market cap.
  /// </summary>
  public int cmc_rank { get; set; }

  /// <summary>
  /// The approximate number of coins circulating for this cryptocurrency.
  /// </summary>
  //public ulong? circulating_supply { get; set; }

  /// <summary>
  /// The approximate total amount of coins in existence right now (minus any coins that have been verifiably burned).
  /// </summary>
  //public ulong? total_supply { get; set; }

  /// <summary>
  /// The expected maximum limit of coins ever to be available for this cryptocurrency.
  /// </summary>
  //public ulong? max_supply { get; set; }

  /// <summary>
  /// Array of tags associated with this cryptocurrency.
  /// </summary>
  public string[] tags = Array.Empty<string>();

  /// <summary>
  /// Timestamp (ISO 8601) of the last time this cryptocurrency's market data was updated.
  /// </summary>
  public DateTime last_updated { get; set; }

  /// <summary>
  /// A map of market quotes in different currency conversions.
  /// </summary>
  public Dictionary<string, CMCQuoteDto> quote { get; set; } = new Dictionary<string, CMCQuoteDto>();
}