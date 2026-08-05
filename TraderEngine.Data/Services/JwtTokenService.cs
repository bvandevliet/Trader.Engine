using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TraderEngine.Data.AppSettings;
using TraderEngine.Data.Entities;

namespace TraderEngine.Data.Services;

/// <summary>
/// Mints the same JWT shape <see cref="AppUser"/>-authenticated callers present to
/// TraderEngine.API, shared by the API's own login endpoint and by TraderEngine.Web (which
/// already holds an authenticated Identity cookie principal and mints a matching token itself
/// rather than round-tripping through a second login call).
/// </summary>
public class JwtTokenService(IOptions<JwtSettings> jwtOptions) : IJwtTokenService
{
  private readonly JwtSettings _jwtSettings = jwtOptions.Value;
  private readonly JsonWebTokenHandler _tokenHandler = new();

  public (string Token, DateTimeOffset ExpiresAt) GenerateToken(AppUser user)
  {
    var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes);

    List<Claim> claims =
    [
      new(ClaimTypes.NameIdentifier, user.Id.ToString()),
      new(ClaimTypes.Name, user.UserName ?? user.Id.ToString()),
    ];

    var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SigningKey));
    var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

    var descriptor = new SecurityTokenDescriptor
    {
      Issuer = _jwtSettings.Issuer,
      Audience = _jwtSettings.Audience,
      Subject = new ClaimsIdentity(claims),
      Expires = expiresAt.UtcDateTime,
      SigningCredentials = credentials,
    };

    return (_tokenHandler.CreateToken(descriptor), expiresAt);
  }
}
