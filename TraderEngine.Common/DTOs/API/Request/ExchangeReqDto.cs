using System.ComponentModel.DataAnnotations;

namespace TraderEngine.Common.DTOs.API.Request;

public class ExchangeReqDto
{
  //[Required]
  //public string ExchangeName { get; set; } = null!;

  [Required]
  public string ApiKey { get; set; } = null!;

  [Required]
  public string ApiSecret { get; set; } = null!;
}
