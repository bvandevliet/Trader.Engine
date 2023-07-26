namespace TraderEngine.CLI.DTOs;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>")]
internal class CMCQuoteDto
{
  /// <summary>
  /// Price in the specified currency for this historical.
  /// </summary>
  public decimal price { get; set; }

  /// <summary>
  /// Rolling 24 hour adjusted volume in the specified currency.
  /// </summary>
  public decimal volume_24h { get; set; }

  /// <summary>
  /// 24 hour change in the specified currencies volume.
  /// </summary>
  public decimal volume_change_24h { get; set; }

  /// <summary>
  /// Market cap in the specified currency.
  /// </summary>
  public decimal market_cap { get; set; }

  /// <summary>
  /// Market cap dominance in the specified currency.
  /// </summary>
  public decimal market_cap_dominance { get; set; }

  /// <summary>
  /// 24 hour change in the specified currency.
  /// </summary>
  public decimal percent_change_24h { get; set; }

  /// <summary>
  /// 7 day change in the specified currency.
  /// </summary>
  public decimal percent_change_7d { get; set; }

  /// <summary>
  /// Timestamp (ISO 8601) of when the conversion currency's current value was referenced.
  /// </summary>
  public DateTime last_updated { get; set; }
}