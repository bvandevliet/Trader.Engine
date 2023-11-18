using System.ComponentModel.DataAnnotations;

namespace TraderEngine.Common.DTOs.API.Request;

public class RebalanceReqDto
{
  [Required]
  public ApiCredReqDto ExchangeApiCred { get; set; } = null!;

  /// <inheritdoc cref="AbsAllocReqDto"/>
  [Required]
  public List<AbsAllocReqDto> NewAbsAllocs { get; set; } = null!;

  /// <inheritdoc cref="AllocDiffReqDto"/>
  public List<AllocDiffReqDto>? AllocDiffs { get; set; } = null;
}