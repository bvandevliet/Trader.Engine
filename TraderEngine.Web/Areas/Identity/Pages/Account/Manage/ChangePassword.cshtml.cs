using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TraderEngine.Data.Entities;

namespace TraderEngine.Web.Areas.Identity.Pages.Account.Manage;

/// <summary>
/// Overrides the Identity UI default so a forced password change (<see cref="AppUser.MustChangePassword"/>,
/// set when an admin creates or resets a user) is cleared here and the user is sent home
/// afterward instead of back to this same settings page.
/// </summary>
public class ChangePasswordModel : PageModel
{
  private readonly UserManager<AppUser> _userManager;
  private readonly SignInManager<AppUser> _signInManager;
  private readonly ILogger<ChangePasswordModel> _logger;

  public ChangePasswordModel(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager, ILogger<ChangePasswordModel> logger)
  {
    _userManager = userManager;
    _signInManager = signInManager;
    _logger = logger;
  }

  [BindProperty]
  public InputModel Input { get; set; } = new();

  [TempData]
  public string? StatusMessage { get; set; }

  public class InputModel
  {
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Current password")]
    public string OldPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(100, ErrorMessage = "{0} must be at least {2} characters long.", MinimumLength = 8)]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirm new password")]
    [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
  }

  public async Task<IActionResult> OnGetAsync()
  {
    var user = await _userManager.GetUserAsync(User);
    if (user is null)
      return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");

    var hasPassword = await _userManager.HasPasswordAsync(user);
    if (!hasPassword)
      return RedirectToPage("./SetPassword");

    return Page();
  }

  public async Task<IActionResult> OnPostAsync()
  {
    if (!ModelState.IsValid)
      return Page();

    var user = await _userManager.GetUserAsync(User);
    if (user is null)
      return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");

    var changePasswordResult = await _userManager.ChangePasswordAsync(user, Input.OldPassword, Input.NewPassword);
    if (!changePasswordResult.Succeeded)
    {
      foreach (var error in changePasswordResult.Errors)
        ModelState.AddModelError(string.Empty, error.Description);

      return Page();
    }

    var wasForced = user.MustChangePassword;

    if (wasForced)
    {
      user.MustChangePassword = false;
      var updateResult = await _userManager.UpdateAsync(user);
      if (!updateResult.Succeeded)
      {
        // If the flag cannot be cleared, surface an error rather than issuing a cookie that
        // disagrees with the database state (which would re-trap the user on next login).
        foreach (var error in updateResult.Errors)
          ModelState.AddModelError(string.Empty, error.Description);

        return Page();
      }
    }

    // Refresh sign-in so the auth cookie reflects the updated claims (MustChangePassword removed).
    await _signInManager.RefreshSignInAsync(user);
    _logger.LogInformation("User changed their password successfully.");

    if (wasForced)
    {
      return LocalRedirect("~/");
    }

    StatusMessage = "Your password has been changed.";
    return RedirectToPage();
  }
}
