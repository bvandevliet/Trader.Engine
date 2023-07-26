namespace TraderEngine.Common.DTOs.Response;

public class AssetData
{
  public string BaseSymbol { get; set; } = null!;

  /// <summary>
  /// The precision used for specifiying amounts.
  /// </summary>
  public int Decimals { get; set; }
}