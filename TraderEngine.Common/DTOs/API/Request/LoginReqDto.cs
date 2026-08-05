using System.ComponentModel.DataAnnotations;

namespace TraderEngine.Common.DTOs.API.Request;

public class LoginReqDto
{
  [Required]
  public string UserName { get; set; } = null!;

  [Required]
  public string Password { get; set; } = null!;
}
