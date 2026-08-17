using System.ComponentModel.DataAnnotations;

namespace TraderEngine.Common.DTOs.API.Request;

public class RebalanceReqDto : SimulationReqDto
{
  [Required]
  public new IEnumerable<TargetAllocReqDto> TargetAllocs { get; set; } = null!;

  public RebalanceReqDto()
  {
  }

  public RebalanceReqDto(
    ApiCredReqDto exchangeApiCred,
    ConfigReqDto config,
    IEnumerable<TargetAllocReqDto> targetAllocs)
  {
    ExchangeApiCred = exchangeApiCred;
    Config = config;
    TargetAllocs = targetAllocs;
  }
}