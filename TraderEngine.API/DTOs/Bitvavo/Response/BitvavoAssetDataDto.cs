namespace TraderEngine.API.DTOs.Bitvavo.Response;

public class BitvavoAssetDataDto
{
  /// <summary>
  /// Short version of the asset name used in market names.
  /// </summary>
  public string Symbol { get; set; } = null!;

  /// <summary>
  /// The full name of the asset.
  /// </summary>
  public string Name { get; set; } = null!;

  /// <summary>
  /// The precision used for specifiying amounts.
  /// </summary>
  public int? Decimals { get; set; }
}