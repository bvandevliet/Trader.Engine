using Microsoft.AspNetCore.Identity;
using TraderEngine.Data.Entities;
using TraderEngine.Data.Services;

namespace TraderEngine.Web.Services;

public class DelegatedAccessResolver : IDelegatedAccessResolver
{
  private readonly IDelegationAuthorizationService _delegationAuth;
  private readonly UserManager<AppUser> _userManager;

  public DelegatedAccessResolver(IDelegationAuthorizationService delegationAuth, UserManager<AppUser> userManager)
  {
    _delegationAuth = delegationAuth;
    _userManager = userManager;
  }

  public async Task<DelegatedAccessContext> ResolveAsync(AppUser caller, Guid? actingAsClientId)
  {
    if (actingAsClientId is null || actingAsClientId == caller.Id)
      return new DelegatedAccessContext(caller, caller, IsDelegated: false);

    if (!await _delegationAuth.IsAuthorizedAsync(caller.Id, actingAsClientId.Value))
      throw new DelegationAccessDeniedException(
        "You do not currently have delegated access to this client's portfolio.");

    var client = await _userManager.FindByIdAsync(actingAsClientId.Value.ToString())
                 ?? throw new DelegationAccessDeniedException(
                   "You do not currently have delegated access to this client's portfolio.");

    return new DelegatedAccessContext(caller, client, IsDelegated: true);
  }
}