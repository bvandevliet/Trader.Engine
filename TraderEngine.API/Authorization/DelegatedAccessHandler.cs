using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using TraderEngine.Data.Constants;
using TraderEngine.Data.Services;

namespace TraderEngine.API.Authorization;

/// <summary>
/// The API's independent re-check of a delegated request, run for every authenticated request via
/// the default authorization policy (see Program.cs). A token with no ActingAsClientId claim
/// (the ordinary case: a user acting on their own account) succeeds trivially, at no DB cost.
/// A token that does carry the claim only succeeds if IDelegationAuthorizationService confirms,
/// against the database right now, that the grant is genuinely active AND the caller still holds
/// the PortfolioManager role — never trusting the claim's mere presence on its own.
/// </summary>
public class DelegatedAccessHandler : AuthorizationHandler<DelegatedAccessRequirement>
{
  private readonly IDelegationAuthorizationService _delegationAuth;

  public DelegatedAccessHandler(IDelegationAuthorizationService delegationAuth)
  {
    _delegationAuth = delegationAuth;
  }

  protected override async Task HandleRequirementAsync(
    AuthorizationHandlerContext context, DelegatedAccessRequirement requirement)
  {
    var actingAsClientIdClaim = context.User.FindFirstValue(DelegationClaimTypes.ActingAsClientId);

    if (actingAsClientIdClaim is null)
    {
      // Ordinary, non-delegated request — the caller is acting on their own account.
      context.Succeed(requirement);
      return;
    }

    var nameIdentifierClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

    if (nameIdentifierClaim is null
      || !Guid.TryParse(nameIdentifierClaim, out var managerId)
      || !Guid.TryParse(actingAsClientIdClaim, out var clientId))
    {
      return;
    }

    if (await _delegationAuth.IsAuthorizedAsync(managerId, clientId))
      context.Succeed(requirement);
  }
}
