using System.ComponentModel.DataAnnotations;
using TraderEngine.Common.DTOs.API.Response;

namespace TraderEngine.Common.DTOs.API.Request;

public class RebalanceReqDto
{
  [Required]
  public ExchangeReqDto Exchange { get; set; } = null!;

  [Required]
  public ConfigReqDto Config { get; set; } = null!;

  /// <inheritdoc cref="AbsAllocReqDto"/>
  [Required]
  public IEnumerable<AbsAllocReqDto> NewAbsAllocs { get; set; } = null!;
}