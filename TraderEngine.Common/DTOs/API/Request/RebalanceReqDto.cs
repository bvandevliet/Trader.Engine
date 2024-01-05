using System.ComponentModel.DataAnnotations;

namespace TraderEngine.Common.DTOs.API.Request;

public class RebalanceReqDto : SimulationReqDto
{
  [Required]
  public new IEnumerable<AbsAllocReqDto> NewAbsAllocs { get; set; } = null!;

  public RebalanceReqDto()
  {
  }

  public RebalanceReqDto(
    ApiCredReqDto exchangeApiCred,
    ConfigReqDto config,
    IEnumerable<AbsAllocReqDto> newAbsAllocs)
  {
    ExchangeApiCred = exchangeApiCred;
    Config = config;
    NewAbsAllocs = newAbsAllocs;
  }
}