using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TraderEngine.Data.Constants;
using TraderEngine.Data.Entities;
using TraderEngine.Data.Repositories;

namespace TraderEngine.Web.Pages;

/// <summary>
/// Self-service portfolio-manager role opt-in/opt-out, plus the two delegation lists: managers
/// this user (as a client) currently grants access to their own portfolio, and clients who
/// currently grant this user (as a manager) access to theirs. Reachable by every authenticated
/// user — anyone can be a client regardless of manager status, and opting in to become a manager
/// happens on this same page.
/// </summary>
public class DelegationModel : TraderEnginePageModelBase
{
  private readonly IPortfolioDelegationRepository _delegationRepository;

  public DelegationModel(UserManager<AppUser> userManager, IPortfolioDelegationRepository delegationRepository)
    : base(userManager)
  {
    _delegationRepository = delegationRepository;
  }

  public bool IsPortfolioManager { get; set; }

  public List<DelegationCounterpartRow> MyManagers { get; set; } = [];

  public List<DelegationCounterpartRow> MyClients { get; set; } = [];

  [BindProperty]
  public string ManagerIdentifier { get; set; } = string.Empty;

  public async Task OnGetAsync()
  {
    var user = await GetCurrentUserAsync();

    IsPortfolioManager = await UserManager.IsInRoleAsync(user, Roles.PortfolioManager);
    MyManagers = await _delegationRepository.GetActiveManagersForClientAsync(user.Id);

    if (IsPortfolioManager)
      MyClients = await _delegationRepository.GetActiveClientsForManagerAsync(user.Id);
  }

  /// <summary>
  /// Self-service, immediate — no approval step. Turning the role off does not revoke any
  /// existing grants (see PortfolioManagerGrant's doc comment): every delegated request
  /// re-verifies live role membership regardless, so this alone already, instantly and correctly,
  /// blocks all delegated access; turning it back on instantly restores every previously-granted
  /// relationship without clients needing to re-grant anything.
  /// </summary>
  public async Task<IActionResult> OnPostToggleManagerRoleAsync()
  {
    var user = await GetCurrentUserAsync();

    var isManager = await UserManager.IsInRoleAsync(user, Roles.PortfolioManager);

    var result = isManager
      ? await UserManager.RemoveFromRoleAsync(user, Roles.PortfolioManager)
      : await UserManager.AddToRoleAsync(user, Roles.PortfolioManager);

    TempData[result.Succeeded ? "Notice" : "Error"] = result.Succeeded
      ? isManager ? "You are no longer a portfolio manager." : "You are now a portfolio manager."
      : string.Join(", ", result.Errors.Select(e => e.Description));

    return RedirectToPage();
  }

  /// <summary>
  /// Exact username/email match only, never a browsable directory — listing every
  /// portfolio-manager-role user to any authenticated caller would be an information-disclosure
  /// surface this feature doesn't need to open, given a manager and client are presumed to
  /// already know each other. Rejects with one deliberately generic message for "not found", "is
  /// yourself" and "not a manager" alike, to avoid account enumeration.
  /// </summary>
  public async Task<IActionResult> OnPostGrantAsync()
  {
    var client = await GetCurrentUserAsync();

    var identifier = ManagerIdentifier.Trim();
    var manager = await UserManager.FindByEmailAsync(identifier) ?? await UserManager.FindByNameAsync(identifier);

    if (manager is null || manager.Id == client.Id || !await UserManager.IsInRoleAsync(manager, Roles.PortfolioManager))
    {
      TempData["Error"] = "No portfolio manager found with that username or email.";
      return RedirectToPage();
    }

    await _delegationRepository.GrantAsync(manager.Id, client.Id);

    TempData["Notice"] = $"Granted \"{manager.DisplayName}\" access to your portfolio.";

    return RedirectToPage();
  }

  /// <summary>
  /// The client unilaterally revoking a manager's access to their own portfolio.
  /// </summary>
  public async Task<IActionResult> OnPostRevokeGrantAsync(Guid managerId)
  {
    var client = await GetCurrentUserAsync();

    await _delegationRepository.RevokeAsync(managerId, client.Id);

    TempData["Notice"] = "Access revoked.";

    return RedirectToPage();
  }

  /// <summary>
  /// The manager unilaterally stepping down from a specific client, without the client having to
  /// act — mirrors the client's own unilateral revoke above.
  /// </summary>
  public async Task<IActionResult> OnPostRevokeClientAsync(Guid clientId)
  {
    var manager = await GetCurrentUserAsync();

    await _delegationRepository.RevokeAsync(manager.Id, clientId);

    TempData["Notice"] = "You no longer manage this client's portfolio.";

    return RedirectToPage();
  }
}