using System.ComponentModel.DataAnnotations;

namespace TraderEngine.Common.DTOs.API.Request;

public class SimulationReqDto
{
  [Required]
  public ApiCredReqDto ExchangeApiCred { get; set; } = null!;

  [Required]
  public ConfigReqDto Config { get; set; } = null!;

  public IEnumerable<TargetAllocReqDto>? TargetAllocs { get; set; }

  public SimulationReqDto()
  {
  }

  public SimulationReqDto(
    ApiCredReqDto exchangeApiCred,
    ConfigReqDto config)
  {
    ExchangeApiCred = exchangeApiCred;
    Config = config;
  }
}