namespace TraderEngine.Data.Constants;

/// <summary>
/// Custom JWT claim types for portfolio-manager delegation, shared by TraderEngine.Web (which
/// mints the claim via JwtTokenService) and TraderEngine.API (which independently re-verifies it
/// before honoring it — see DelegatedAccessHandler).
/// </summary>
public static class DelegationClaimTypes
{
  /// <summary>
  /// Present only when a portfolio manager is acting on a client's account via a delegation
  /// grant; holds the target client's userId. <see cref="System.Security.Claims.ClaimTypes.NameIdentifier"/>
  /// keeps its existing, unmodified meaning — the real authenticated caller's own userId — in
  /// every token, delegated or not. Any endpoint that never reads this claim (e.g.
  /// ApiCredentialsController) is therefore safe by construction: it always resolves the caller's
  /// own account, never a delegated client's, regardless of what this claim says.
  /// </summary>
  public const string ActingAsClientId = "acting_as_client_id";
}
