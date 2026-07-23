using Microsoft.AspNetCore.Identity;

namespace TraderEngine.Data.Entities;

public class AppUser : IdentityUser<Guid>
{
  public string DisplayName { get; set; } = string.Empty;
}
