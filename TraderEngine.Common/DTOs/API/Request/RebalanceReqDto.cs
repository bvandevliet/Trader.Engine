using System.ComponentModel.DataAnnotations;

namespace TraderEngine.Common.DTOs.API.Request;

public class RebalanceReqDto
{
  [Required]
  public ExchangeReqDto Exchange { get; set; } = null!;

  [Required]
  public ConfigReqDto Config { get; set; } = null!;

  /// <inheritdoc cref="AbsAllocReqDto"/>
  [Required]
  public List<AbsAllocReqDto> NewAbsAllocs { get; set; } = null!;

  /// <inheritdoc cref="AllocDiffReqDto"/>
  public List<AllocDiffReqDto>? AllocDiffs { get; set; } = null;
}