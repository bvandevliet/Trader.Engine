using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TraderEngine.API.AppSettings;
using TraderEngine.Data.Constants;
using TraderEngine.Data.Entities;

namespace TraderEngine.API.Data;

/// <summary>
/// Seeds roles and the single bootstrap operator account on startup. Called once from
/// <c>Program.cs</c> after migrations run.
/// </summary>
public static class DbInitializer
{
  public static async Task InitializeAsync(IServiceProvider services)
  {
    await SeedRoles(services);
    await SeedAdminUser(services);
  }

  /// <summary>
  /// Idempotently ensures every role in <see cref="Roles.All"/> exists, and removes any stale
  /// role no longer in that list — keeps AspNetRoles in sync with the fixed set of application
  /// roles rather than requiring a manual migration whenever that set changes.
  /// </summary>
  private static async Task SeedRoles(IServiceProvider services)
  {
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

    var existingRoles = await roleManager.Roles.ToListAsync();

    foreach (var staleRole in existingRoles.Where(r => !Roles.All.Contains(r.Name)))
      await roleManager.DeleteAsync(staleRole);

    foreach (var roleName in Roles.All.Where(r => existingRoles.All(er => er.Name != r)))
      await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
  }

  /// <summary>
  /// Idempotently creates the single operator account from <see cref="AdminSeedSettings"/> if not
  /// already present (never overwrites an existing user's password/email/display name), and — on
  /// every startup, regardless of whether the account already existed — ensures it holds the
  /// Admin role. That second part matters for databases that already had a seeded operator
  /// account from before roles existed: without it, that account would silently end up with zero
  /// roles once <see cref="Policies.AdminOnly"/> shipped, with no other Admin account able to
  /// promote it (the chicken-and-egg problem this settings-driven bootstrap exists to avoid).
  /// </summary>
  private static async Task SeedAdminUser(IServiceProvider services)
  {
    var seedSettings = services.GetRequiredService<IOptions<AdminSeedSettings>>().Value;

    if (string.IsNullOrEmpty(seedSettings.UserName))
    {
      return;
    }

    var userManager = services.GetRequiredService<UserManager<AppUser>>();

    var existingUser = await userManager.FindByNameAsync(seedSettings.UserName);
    if (existingUser != null)
    {
      if (!await userManager.IsInRoleAsync(existingUser, Roles.Admin))
        await userManager.AddToRoleAsync(existingUser, Roles.Admin);

      return;
    }

    if (string.IsNullOrEmpty(seedSettings.Email) ||
      string.IsNullOrEmpty(seedSettings.Password))
    {
      return;
    }

    var user = new AppUser
    {
      UserName = seedSettings.UserName,
      Email = seedSettings.Email,
      DisplayName = seedSettings.UserName,
      EmailConfirmed = true,
    };

    var result = await userManager.CreateAsync(user, seedSettings.Password);

    if (result.Succeeded)
    {
      await userManager.AddToRoleAsync(user, Roles.Admin);
    }
    else
    {
      var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DbInitializer));
      logger.LogCritical("Failed to seed admin user: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
    }
  }
}
