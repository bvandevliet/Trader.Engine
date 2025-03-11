namespace TraderEngine.Common.DTOs.API.Response;

public class AssetDataDto
{
  /// <summary>
  /// Short version of the asset name used in market names.
  /// </summary>
  public string BaseSymbol { get; set; } = null!;

  /// <summary>
  /// The full name of the asset.
  /// </summary>
  public string Name { get; set; } = null!;

  /// <summary>
  /// The precision used for specifiying amounts.
  /// </summary>
  public int Decimals { get; set; }
}