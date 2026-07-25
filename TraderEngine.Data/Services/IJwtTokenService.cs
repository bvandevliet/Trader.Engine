using TraderEngine.Data.Entities;

namespace TraderEngine.Data.Services;

public interface IJwtTokenService
{
  public (string Token, DateTimeOffset ExpiresAt) GenerateToken(AppUser user);
}
