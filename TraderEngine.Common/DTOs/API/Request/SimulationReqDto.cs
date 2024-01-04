using System.ComponentModel.DataAnnotations;

namespace TraderEngine.Common.DTOs.API.Request;

public class SimulationReqDto
{
  [Required]
  public ApiCredReqDto ExchangeApiCred { get; set; } = null!;

  [Required]
  public List<AbsAllocReqDto> NewAbsAllocs { get; set; } = null!;

  [Required]
  public ConfigReqDto Config { get; set; } = null!;

  public SimulationReqDto()
  {
  }

  public SimulationReqDto(
    ApiCredReqDto exchangeApiCred,
    List<AbsAllocReqDto> newAbsAllocs,
    ConfigReqDto config)
  {
    ExchangeApiCred = exchangeApiCred;
    NewAbsAllocs = newAbsAllocs;
    Config = config;
  }
}