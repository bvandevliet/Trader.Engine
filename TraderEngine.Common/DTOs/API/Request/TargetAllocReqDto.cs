using System.ComponentModel.DataAnnotations;
using TraderEngine.Common.Enums;

namespace TraderEngine.Common.DTOs.API.Request;

public class TargetAllocReqDto
{
  [Required]
  public MarketReqDto Market { get; set; } = null!;

  [Required]
  public decimal TargetWeight { get; set; }

  public MarketStatus MarketStatus { get; set; } = MarketStatus.Unknown;

  public TargetAllocReqDto()
  {
  }

  /// <param name="market"><inheritdoc cref="BaseSymbol"/></param>
  /// <param name="targetAlloc"><inheritdoc cref="TargetWeight"/></param>
  public TargetAllocReqDto(MarketReqDto market, decimal targetAlloc)
  {
    Market = market;
    TargetWeight = targetAlloc;
  }
}