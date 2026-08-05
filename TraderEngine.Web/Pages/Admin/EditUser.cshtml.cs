using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TraderEngine.Data.Constants;
using TraderEngine.Data.Entities;
using TraderEngine.Web.Models;

namespace TraderEngine.Web.Pages.Admin;

/// <summary>
/// Lets an Admin edit a user's username/display name/roles, reset their password, or delete the
/// account. Self-demotion out of the Admin role is blocked so an admin can never accidentally
/// lock themselves out of this page; self-deletion is blocked on <see cref="UsersModel"/>.
/// </summary>
[Authorize(Policy = Policies.AdminOnly)]
public class EditUserModel : TraderEnginePageModelBase
{
  public EditUserModel(UserManager<AppUser> userManager)
    : base(userManager)
  {
  }

  [BindProperty]
  public Guid Id { get; set; }

  [BindProperty]
  public UserEditInput Input { get; set; } = new();

  public string? CurrentFilter { get; set; }

  public IReadOnlyList<RoleSelectionOption> AvailableRoles =>
    Roles.All.Select(role => new RoleSelectionOption
    {
      RoleName = role,
      IsSelected = Input.AssignedRoles.Contains(role),
    }).ToList();

  public async Task<IActionResult> OnGetAsync(Guid id, string? currentFilter)
  {
    var user = await UserManager.FindByIdAsync(id.ToString());
    if (user is null)
      return NotFound();

    Id = user.Id;
    Input.UserName = user.UserName ?? string.Empty;
    Input.Email = user.Email ?? string.Empty;
    Input.DisplayName = user.DisplayName;
    Input.AssignedRoles = (await UserManager.GetRolesAsync(user)).ToList();
    CurrentFilter = currentFilter;

    return Page();
  }

  public async Task<IActionResult> OnPostAsync(string? currentFilter)
  {
    CurrentFilter = currentFilter;

    var user = await UserManager.FindByIdAsync(Id.ToString());
    if (user is null)
      return NotFound();

    var currentUser = await GetCurrentUserAsync();

    // Prevent an admin from removing their own Admin role, which would lock them out of this page.
    if (currentUser.Id == user.Id && !Input.AssignedRoles.Contains(Roles.Admin))
    {
      ModelState.AddModelError(string.Empty, "You cannot remove your own Admin role.");
    }

    if (Input.AssignedRoles.Any(roleName => !Roles.All.Contains(roleName)))
    {
      ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.AssignedRoles)}", "Invalid role selected.");
    }

    if (!ModelState.IsValid)
      return Page();

    // Update username.
    if (!string.Equals(user.UserName, Input.UserName, StringComparison.OrdinalIgnoreCase))
    {
      var setUserNameResult = await UserManager.SetUserNameAsync(user, Input.UserName);
      if (!setUserNameResult.Succeeded)
      {
        foreach (var error in setUserNameResult.Errors)
        {
          ModelState.AddModelError(error.Code == "DuplicateUserName" ? $"{nameof(Input)}.{nameof(Input.UserName)}" : string.Empty,
            error.Code == "DuplicateUserName" ? "This username is already in use." : error.Description);
        }

        return Page();
      }
    }

    // Update email.
    if (!string.Equals(user.Email, Input.Email, StringComparison.OrdinalIgnoreCase))
    {
      var setEmailResult = await UserManager.SetEmailAsync(user, Input.Email);
      if (!setEmailResult.Succeeded)
      {
        foreach (var error in setEmailResult.Errors)
        {
          ModelState.AddModelError(error.Code == "DuplicateEmail" ? $"{nameof(Input)}.{nameof(Input.Email)}" : string.Empty,
            error.Code == "DuplicateEmail" ? "This email is already in use." : error.Description);
        }

        return Page();
      }
    }

    // Update display name.
    user.DisplayName = Input.DisplayName;
    var updateResult = await UserManager.UpdateAsync(user);
    if (!updateResult.Succeeded)
    {
      foreach (var error in updateResult.Errors)
        ModelState.AddModelError(string.Empty, error.Description);

      return Page();
    }

    // Update roles.
    var currentRoles = await UserManager.GetRolesAsync(user);
    var removeResult = await UserManager.RemoveFromRolesAsync(user, currentRoles);
    if (!removeResult.Succeeded)
    {
      foreach (var error in removeResult.Errors)
        ModelState.AddModelError(string.Empty, error.Description);

      return Page();
    }

    if (Input.AssignedRoles.Count > 0)
    {
      var addResult = await UserManager.AddToRolesAsync(user, Input.AssignedRoles);
      if (!addResult.Succeeded)
      {
        foreach (var error in addResult.Errors)
          ModelState.AddModelError(string.Empty, error.Description);

        return Page();
      }
    }

    // Reset password if a new one was provided.
    if (!string.IsNullOrWhiteSpace(Input.NewPassword))
    {
      var token = await UserManager.GeneratePasswordResetTokenAsync(user);
      var passwordResult = await UserManager.ResetPasswordAsync(user, token, Input.NewPassword);
      if (!passwordResult.Succeeded)
      {
        foreach (var error in passwordResult.Errors)
          ModelState.AddModelError(string.Empty, error.Description);

        return Page();
      }

      // Force the user to change this admin-assigned password on their next login.
      user.MustChangePassword = true;
      await UserManager.UpdateAsync(user);
    }

    TempData["Notice"] = $"User \"{Input.UserName}\" updated.";

    return RedirectToPage("/Admin/Users", new { currentFilter });
  }
}
