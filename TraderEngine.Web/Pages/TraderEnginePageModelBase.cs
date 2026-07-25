using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TraderEngine.Data.Entities;

namespace TraderEngine.Web.Pages;

public abstract class TraderEnginePageModelBase : PageModel
{
  protected readonly UserManager<AppUser> UserManager;

  protected TraderEnginePageModelBase(UserManager<AppUser> userManager)
  {
    UserManager = userManager;
  }

  protected async Task<AppUser> GetCurrentUserAsync() =>
    await UserManager.GetUserAsync(User)
      ?? throw new InvalidOperationException("No authenticated user found for a handler requiring one.");
}
