using System.ComponentModel.DataAnnotations;
using TraderEngine.Common.Enums;

namespace TraderEngine.Common.DTOs.API.Request;

public class AbsAllocReqDto
{
  [Required]
  public MarketReqDto Market { get; set; } = null!;

  [Required]
  public decimal AbsAlloc { get; set; }

  public MarketStatus MarketStatus { get; set; } = MarketStatus.Unknown;

  public AbsAllocReqDto()
  {
  }

  /// <param name="market"><inheritdoc cref="BaseSymbol"/></param>
  /// <param name="absAlloc"><inheritdoc cref="AbsAlloc"/></param>
  public AbsAllocReqDto(MarketReqDto market, decimal absAlloc)
  {
    Market = market;
    AbsAlloc = absAlloc;
  }
}