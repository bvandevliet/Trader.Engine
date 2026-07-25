using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TraderEngine.Data.AppSettings;
using TraderEngine.Data.Entities;

namespace TraderEngine.Data.Services;

/// <summary>
/// Mints the same JWT shape <see cref="Entities.AppUser"/>-authenticated callers present to
/// TraderEngine.API, shared by the API's own login endpoint and by TraderEngine.Web (which
/// already holds an authenticated Identity cookie principal and mints a matching token itself
/// rather than round-tripping through a second login call).
/// </summary>
public class JwtTokenService : IJwtTokenService
{
  private readonly JwtSettings _jwtSettings;

  public JwtTokenService(IOptions<JwtSettings> jwtOptions)
  {
    _jwtSettings = jwtOptions.Value;
  }

  public (string Token, DateTimeOffset ExpiresAt) GenerateToken(AppUser user)
  {
    var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes);

    var claims = new List<Claim>
    {
      new(ClaimTypes.NameIdentifier, user.Id.ToString()),
      new(ClaimTypes.Name, user.UserName ?? user.Id.ToString()),
    };

    var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SigningKey));
    var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
      issuer: _jwtSettings.Issuer,
      audience: _jwtSettings.Audience,
      claims: claims,
      expires: expiresAt.UtcDateTime,
      signingCredentials: credentials);

    return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
  }
}
