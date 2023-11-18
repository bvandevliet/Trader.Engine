using System.ComponentModel.DataAnnotations;

namespace TraderEngine.Common.DTOs.API.Request;

public class ApiCredReqDto
{
  [Required]
  public string ApiKey { get; set; } = null!;

  [Required]
  public string ApiSecret { get; set; } = null!;
}
