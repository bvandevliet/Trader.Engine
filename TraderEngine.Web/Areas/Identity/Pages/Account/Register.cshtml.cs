using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TraderEngine.Data.Constants;
using TraderEngine.Data.Entities;
using TraderEngine.Web.Models;

namespace TraderEngine.Web.Areas.Identity.Pages.Account;

/// <summary>
/// Admin-only user registration — there is no anonymous sign-up flow for this app (see
/// Program.cs's AuthorizeAreaPage convention for this page, and the FallbackPolicy every other
/// page inherits). The <see cref="AuthorizeAttribute"/> here is belt-and-suspenders in case the
/// page is ever reached through a route not covered by that convention.
/// </summary>
[Authorize(Policy = Policies.AdminOnly)]
public class RegisterModel : PageModel
{
  private readonly UserManager<AppUser> _userManager;
  private readonly ILogger<RegisterModel> _logger;

  public RegisterModel(UserManager<AppUser> userManager, ILogger<RegisterModel> logger)
  {
    _userManager = userManager;
    _logger = logger;
  }

  [BindProperty]
  public UserRegistrationInput Input { get; set; } = new();

  public IReadOnlyList<RoleSelectionOption> AvailableRoles =>
    Roles.All.Select(role => new RoleSelectionOption
    {
      RoleName = role,
      IsSelected = Input.AssignedRoles.Contains(role),
    }).ToList();

  public void OnGet()
  {
  }

  public async Task<IActionResult> OnPostAsync()
  {
    if (ModelState.IsValid)
    {
      if (Input.AssignedRoles.Any(roleName => !Roles.All.Contains(roleName)))
      {
        ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.AssignedRoles)}", "Invalid role selected.");
        return Page();
      }

      var user = new AppUser
      {
        UserName = Input.UserName,
        Email = Input.Email,
        DisplayName = Input.DisplayName,
        EmailConfirmed = true,
        MustChangePassword = true,
      };

      var result = await _userManager.CreateAsync(user, Input.Password);

      if (result.Succeeded)
      {
        _logger.LogInformation("User {UserName} registered by admin {AdminName}.", Input.UserName, User.Identity?.Name);

        if (Input.AssignedRoles.Count > 0)
        {
          var addRolesResult = await _userManager.AddToRolesAsync(user, Input.AssignedRoles);
          if (!addRolesResult.Succeeded)
          {
            foreach (var error in addRolesResult.Errors)
              ModelState.AddModelError(string.Empty, error.Description);

            return Page();
          }
        }

        TempData["Notice"] = $"User \"{Input.UserName}\" created.";

        return RedirectToPage("/Admin/Users", new { area = "" });
      }

      foreach (var error in result.Errors)
        ModelState.AddModelError(string.Empty, error.Description);
    }

    // If we got this far, something failed, redisplay form.
    return Page();
  }
}
