using System.ComponentModel.DataAnnotations;

namespace TraderEngine.Common.DTOs.Request;

public class AbsAssetAllocDto
{
  [Required]
  public string BaseSymbol { get; set; } = null!;

  [Required]
  public decimal AbsAlloc { get; set; }

  public AbsAssetAllocDto()
  {
  }

  /// <param name="baseSymbol"><inheritdoc cref="BaseSymbol"/></param>
  /// <param name="absAlloc"><inheritdoc cref="AbsAlloc"/></param>
  public AbsAssetAllocDto(string baseSymbol, decimal absAlloc)
  {
    BaseSymbol = baseSymbol;
    AbsAlloc = absAlloc;
  }
}