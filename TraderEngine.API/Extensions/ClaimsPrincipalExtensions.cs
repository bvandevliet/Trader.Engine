using System.Security.Claims;
using TraderEngine.Data.Constants;

namespace TraderEngine.API.Extensions;

public static class ClaimsPrincipalExtensions
{
  /// <summary>
  /// The userId a per-user request is scoped to: the ActingAsClientId claim when present (a
  /// portfolio manager acting on a client's account — by the time a controller runs, the default
  /// authorization policy's DelegatedAccessHandler has already independently verified this claim
  /// against the database), otherwise the caller's own NameIdentifier.
  /// </summary>
  public static Guid GetEffectiveUserId(this ClaimsPrincipal principal)
  {
    var actingAsClientId = principal.FindFirstValue(DelegationClaimTypes.ActingAsClientId);

    return Guid.Parse(actingAsClientId ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
  }
}
