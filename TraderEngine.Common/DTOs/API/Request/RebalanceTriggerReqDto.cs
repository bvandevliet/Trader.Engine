using System.ComponentModel.DataAnnotations;

namespace TraderEngine.Common.DTOs.API.Request;

public class RebalanceTriggerReqDto
{
  /// <inheritdoc cref="AbsAssetAllocReqDto"/>
  [Required]
  public IEnumerable<AbsAssetAllocReqDto> NewAssetAllocs { get; set; } = null!;

  /// <inheritdoc cref="AllocationReqDto"/>
  public IEnumerable<KeyValuePair<AllocationReqDto, decimal>>? AllocQuoteDiffs { get; set; } = null;
}