namespace TraderEngine.Common.DTOs.API.Response;

public class LoginResponseDto
{
  public string Token { get; set; } = null!;

  public DateTimeOffset ExpiresAt { get; set; }
}
