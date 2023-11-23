using System.ComponentModel.DataAnnotations;

namespace TraderEngine.Common.DTOs.API.Request;

public class AbsAllocReqDto
{
  [Required]
  public string BaseSymbol { get; set; } = null!;

  [Required]
  public decimal AbsAlloc { get; set; }

  public AbsAllocReqDto()
  {
  }

  /// <param name="baseSymbol"><inheritdoc cref="BaseSymbol"/></param>
  /// <param name="absAlloc"><inheritdoc cref="AbsAlloc"/></param>
  public AbsAllocReqDto(string baseSymbol, decimal absAlloc)
  {
    BaseSymbol = baseSymbol;
    AbsAlloc = absAlloc;
  }
}