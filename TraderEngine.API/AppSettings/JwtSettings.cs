namespace TraderEngine.API.AppSettings;

public class JwtSettings
{
  public string Issuer { get; set; } = "TraderEngine";

  public string Audience { get; set; } = "TraderEngine";

  public string SigningKey { get; set; } = string.Empty;

  public int ExpiryMinutes { get; set; } = 60;
}
