using TraderEngine.Data.Entities;

namespace TraderEngine.Data.Services;

public interface IJwtTokenService
{
  /// <summary>
  /// Mints a token authenticating <paramref name="user"/> — <see
  /// cref="System.Security.Claims.ClaimTypes.NameIdentifier"/> is always <paramref name="user"/>'s
  /// own id. When <paramref name="actingAsClientId"/> is supplied (a portfolio manager acting on
  /// a client's account), it's embedded as a separate claim
  /// (<see cref="TraderEngine.Data.Constants.DelegationClaimTypes.ActingAsClientId"/>) rather than
  /// replacing NameIdentifier — see that claim's doc comment for why.
  /// </summary>
  public (string Token, DateTimeOffset ExpiresAt) GenerateToken(AppUser user, Guid? actingAsClientId = null);
}