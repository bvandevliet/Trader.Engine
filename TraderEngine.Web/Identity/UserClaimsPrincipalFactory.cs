using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using TraderEngine.Data.Entities;

namespace TraderEngine.Web.Identity;

/// <summary>
/// Extends the default claims principal factory to embed application-specific claims (e.g.
/// <see cref="AppClaimTypes.MustChangePassword"/>) into the auth cookie. This avoids a database
/// round-trip on every request when checking this flag.
/// Must extend <see cref="UserClaimsPrincipalFactory{TUser,TRole}"/> (not the single-type
/// overload) so that role claims continue to be included alongside it.
/// </summary>
public class UserClaimsPrincipalFactory(
  UserManager<AppUser> userManager,
  RoleManager<IdentityRole<Guid>> roleManager,
  IOptions<IdentityOptions> optionsAccessor)
  : UserClaimsPrincipalFactory<AppUser, IdentityRole<Guid>>(userManager, roleManager, optionsAccessor)
{
  protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
  {
    var identity = await base.GenerateClaimsAsync(user);

    if (user.MustChangePassword)
    {
      identity.AddClaim(new Claim(AppClaimTypes.MustChangePassword, bool.TrueString));
    }

    return identity;
  }
}
