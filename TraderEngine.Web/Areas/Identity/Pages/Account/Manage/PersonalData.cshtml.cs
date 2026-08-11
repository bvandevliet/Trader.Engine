using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TraderEngine.Data.Entities;

namespace TraderEngine.Web.Areas.Identity.Pages.Account.Manage;

/// <summary>
/// Landing page linking to the framework-provided DownloadPersonalData/DeletePersonalData
/// handlers; overridden here purely to control its markup/styling like the rest of Manage.
/// </summary>
public class PersonalDataModel : PageModel
{
  private readonly UserManager<AppUser> _userManager;

  public PersonalDataModel(UserManager<AppUser> userManager)
  {
    _userManager = userManager;
  }

  public async Task<IActionResult> OnGetAsync()
  {
    var user = await _userManager.GetUserAsync(User);
    if (user is null)
      return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");

    return Page();
  }
}
