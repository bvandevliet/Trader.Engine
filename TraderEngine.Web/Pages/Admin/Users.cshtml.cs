using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TraderEngine.Data;
using TraderEngine.Data.Constants;
using TraderEngine.Data.Entities;
using TraderEngine.Web.Models;

namespace TraderEngine.Web.Pages.Admin;

/// <summary>
/// Paginated, searchable overview of all users with their roles, lockout status and login
/// activity. Editing/deleting/(un)locking a specific user happens on <see cref="EditUserModel"/>
/// and the handlers below; new users are created via the admin-only Identity Register page.
/// </summary>
[Authorize(Policy = Policies.AdminOnly)]
public class UsersModel : TraderEnginePageModelBase
{
  private const int PageSize = 25;

  private readonly TraderEngineDbContext _context;

  public UsersModel(UserManager<AppUser> userManager, TraderEngineDbContext context)
    : base(userManager)
  {
    _context = context;
  }

  public PaginatedList<UserRow> Users { get; set; } = PaginatedList<UserRow>.Create([], 0, 1, PageSize);

  public string? CurrentFilter { get; set; }

  public async Task OnGetAsync(string? currentFilter, string? searchString, int? pageNumber)
  {
    // New search resets to page 1; paging preserves the previous filter.
    if (searchString != null)
      pageNumber = 1;
    else
      searchString = currentFilter;

    CurrentFilter = searchString;

    var query = UserManager.Users.AsNoTracking();

    if (!string.IsNullOrWhiteSpace(searchString))
    {
      var keywords = searchString.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

      foreach (var keyword in keywords)
      {
        query = query.Where(u =>
          u.UserName != null && EF.Functions.ILike(u.UserName, $"%{keyword}%") ||
          EF.Functions.ILike(u.DisplayName, $"%{keyword}%") ||
          u.Email != null && EF.Functions.ILike(u.Email, $"%{keyword}%"));
      }
    }

    query = query.OrderBy(u => u.UserName);

    var pageIndex = Math.Max(1, pageNumber ?? 1);
    var totalCount = await query.CountAsync();

    var users = await query
      .Skip((pageIndex - 1) * PageSize)
      .Take(PageSize)
      .ToListAsync();

    // Load role mappings only for the current page of users.
    var userIds = users.Select(u => u.Id).ToHashSet();
    var rolesByUserId = await _context.UserRoles
      .AsNoTracking()
      .Where(ur => userIds.Contains(ur.UserId))
      .Join(_context.Roles.AsNoTracking(),
        ur => ur.RoleId, r => r.Id,
        (ur, r) => new { ur.UserId, RoleName = r.Name })
      .Where(x => !string.IsNullOrWhiteSpace(x.RoleName))
      .GroupBy(x => x.UserId)
      .ToDictionaryAsync(g => g.Key, g => g.Select(x => x.RoleName!).ToList());

    var rows = users.Select(user => new UserRow
    {
      Id = user.Id,
      UserName = user.UserName ?? string.Empty,
      Email = user.Email ?? string.Empty,
      DisplayName = user.DisplayName,
      Roles = rolesByUserId.GetValueOrDefault(user.Id, []).OrderBy(r => r).ToList(),
      LockedOut = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow,
      LoginCount = user.LoginCount,
      LastLoginAt = user.LastLoginAt,
      MustChangePassword = user.MustChangePassword,
    }).ToList();

    Users = PaginatedList<UserRow>.Create(rows, totalCount, pageIndex, PageSize);
  }

  /// <summary>
  /// Toggles user lockout status. Prevents an admin from locking themselves out.
  /// </summary>
  public async Task<IActionResult> OnPostToggleLockoutAsync(Guid id)
  {
    var user = await UserManager.FindByIdAsync(id.ToString());
    if (user is null)
      return NotFound();

    var currentUser = await GetCurrentUserAsync();
    if (currentUser.Id == user.Id)
    {
      TempData["Error"] = "You cannot lock your own account.";
      return RedirectToPage();
    }

    var isLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;

    // Unlock immediately, or lock for 100 years (effectively permanent).
    var result = await UserManager.SetLockoutEndDateAsync(user, isLockedOut ? null : DateTimeOffset.UtcNow.AddYears(100));

    TempData[result.Succeeded ? "Notice" : "Error"] = result.Succeeded
      ? $"User \"{user.UserName}\" {(isLockedOut ? "unlocked" : "locked")}."
      : string.Join(", ", result.Errors.Select(e => e.Description));

    return RedirectToPage();
  }

  /// <summary>
  /// Deletes a user account. Prevents self-deletion.
  /// </summary>
  public async Task<IActionResult> OnPostDeleteUserAsync(Guid id)
  {
    var user = await UserManager.FindByIdAsync(id.ToString());
    if (user is null)
      return NotFound();

    var currentUser = await GetCurrentUserAsync();
    if (currentUser.Id == user.Id)
    {
      TempData["Error"] = "You cannot delete your own account.";
      return RedirectToPage();
    }

    var result = await UserManager.DeleteAsync(user);

    TempData[result.Succeeded ? "Notice" : "Error"] = result.Succeeded
      ? $"User \"{user.UserName}\" deleted."
      : string.Join(", ", result.Errors.Select(e => e.Description));

    return RedirectToPage();
  }
}
