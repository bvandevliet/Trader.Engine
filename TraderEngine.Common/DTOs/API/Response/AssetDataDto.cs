namespace TraderEngine.Common.DTOs.API.Response;

public class AssetDataDto
{
  public string BaseSymbol { get; set; } = null!;

  /// <summary>
  /// The precision used for specifiying amounts.
  /// </summary>
  public int Decimals { get; set; }
}