namespace TraderEngine.Data.Services;

/// <summary>
/// The single re-usable "is this manager currently allowed to act on this client's account"
/// check, consulted independently by both hosts: TraderEngine.Web (before minting a delegated
/// JWT) and TraderEngine.API (DelegatedAccessHandler, re-verifying against the database rather
/// than trusting the token's claim alone).
/// </summary>
public interface IDelegationAuthorizationService
{
  /// <summary>
  /// True only when BOTH an active grant exists for this exact pair AND <paramref name="managerId"/>
  /// currently holds the PortfolioManager role — a role removed after a grant was made (or after
  /// a token was minted) fails this check immediately, without needing the grant itself touched.
  /// </summary>
  Task<bool> IsAuthorizedAsync(Guid managerId, Guid clientId);
}