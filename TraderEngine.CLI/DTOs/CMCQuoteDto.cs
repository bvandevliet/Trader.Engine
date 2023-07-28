namespace TraderEngine.CLI.DTOs;

internal class CMCQuoteDto
{
  /// <summary>
  /// Price in the specified currency for this historical.
  /// </summary>
  public decimal Price { get; set; }

  /// <summary>
  /// Rolling 24 hour adjusted volume in the specified currency.
  /// </summary>
  //public decimal Volume_24h { get; set; }

  /// <summary>
  /// 24 hour change in the specified currencies volume.
  /// </summary>
  //public decimal Volume_Change_24h { get; set; }

  /// <summary>
  /// Market cap in the specified currency.
  /// </summary>
  public decimal Market_Cap { get; set; }

  /// <summary>
  /// Market cap dominance in the specified currency.
  /// </summary>
  public decimal Market_Cap_Dominance { get; set; }

  /// <summary>
  /// 24 hour change in the specified currency.
  /// </summary>
  //public decimal Percent_Change_24h { get; set; }

  /// <summary>
  /// 7 day change in the specified currency.
  /// </summary>
  //public decimal Percent_Change_7d { get; set; }

  /// <summary>
  /// Timestamp (ISO 8601) of when the conversion currency's current value was referenced.
  /// </summary>
  public DateTime Last_Updated { get; set; }
}