using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TraderEngine.Data.Entities;

namespace TraderEngine.Web.Areas.Identity.Pages.Account.Manage;

/// <summary>
/// Lets the signed-in user view their username and edit their display name.
/// </summary>
public class IndexModel : PageModel
{
  private readonly UserManager<AppUser> _userManager;
  private readonly SignInManager<AppUser> _signInManager;

  public IndexModel(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager)
  {
    _userManager = userManager;
    _signInManager = signInManager;
  }

  public string Username { get; set; } = string.Empty;

  [TempData]
  public string? StatusMessage { get; set; }

  [BindProperty]
  public InputModel Input { get; set; } = new();

  public class InputModel
  {
    [Required(ErrorMessage = "Display name is required.")]
    [StringLength(100, ErrorMessage = "{0} must be at least {2} characters long.", MinimumLength = 1)]
    [Display(Name = "Display name")]
    public string DisplayName { get; set; } = string.Empty;
  }

  private async Task LoadAsync(AppUser user)
  {
    Username = await _userManager.GetUserNameAsync(user) ?? string.Empty;

    Input = new InputModel
    {
      DisplayName = user.DisplayName,
    };
  }

  public async Task<IActionResult> OnGetAsync()
  {
    var user = await _userManager.GetUserAsync(User);
    if (user is null)
      return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");

    await LoadAsync(user);
    return Page();
  }

  public async Task<IActionResult> OnPostAsync()
  {
    var user = await _userManager.GetUserAsync(User);
    if (user is null)
      return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");

    if (!ModelState.IsValid)
    {
      await LoadAsync(user);
      return Page();
    }

    if (Input.DisplayName != user.DisplayName)
    {
      user.DisplayName = Input.DisplayName;
      var result = await _userManager.UpdateAsync(user);
      if (!result.Succeeded)
      {
        StatusMessage = "Error: unexpected error setting display name.";
        return RedirectToPage();
      }

      await _signInManager.RefreshSignInAsync(user);
    }

    StatusMessage = "Your profile has been updated.";
    return RedirectToPage();
  }
}
