namespace TraderEngine.CLI.DTOs;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>")]
internal class CMCListLatestDto
{
  /// <summary>
  /// Array of cryptocurrency objects matching the list options.
  /// </summary>
  public CMCAssetDto[] data { get; set; } = Array.Empty<CMCAssetDto>();
}