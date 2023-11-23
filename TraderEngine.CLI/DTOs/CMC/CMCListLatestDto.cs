namespace TraderEngine.CLI.DTOs.CMC;

internal class CMCListLatestDto
{
  /// <summary>
  /// Array of cryptocurrency objects matching the list options.
  /// </summary>
  public CMCAssetDto[] Data { get; set; } = Array.Empty<CMCAssetDto>();
}