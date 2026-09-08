using Microsoft.AspNetCore.Identity;
using TraderEngine.Data.Constants;
using TraderEngine.Data.Entities;
using TraderEngine.Data.Repositories;

namespace TraderEngine.Data.Services;

public class DelegationAuthorizationService : IDelegationAuthorizationService
{
  private readonly IPortfolioDelegationRepository _delegationRepository;
  private readonly UserManager<AppUser> _userManager;

  public DelegationAuthorizationService(IPortfolioDelegationRepository delegationRepository,
    UserManager<AppUser> userManager)
  {
    _delegationRepository = delegationRepository;
    _userManager = userManager;
  }

  public async Task<bool> IsAuthorizedAsync(Guid managerId, Guid clientId)
  {
    if (managerId == clientId)
      return false;

    if (!await _delegationRepository.HasActiveGrantAsync(managerId, clientId))
      return false;

    var manager = await _userManager.FindByIdAsync(managerId.ToString());

    return manager != null && await _userManager.IsInRoleAsync(manager, Roles.PortfolioManager);
  }
}