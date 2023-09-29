using System.ComponentModel.DataAnnotations;
using TraderEngine.Common.DTOs.API.Response;

namespace TraderEngine.Common.DTOs.API.Request;

public class RebalanceReqDto
{
  /// <inheritdoc cref="AbsAllocReqDto"/>
  [Required]
  public IEnumerable<AbsAllocReqDto> NewAbsAllocs { get; set; } = null!;

  /// <inheritdoc cref="AllocationDto"/>
  public IEnumerable<KeyValuePair<AllocationDto, decimal>>? AllocQuoteDiffs { get; set; } = null;
}