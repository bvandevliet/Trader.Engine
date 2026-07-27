using Microsoft.AspNetCore.Identity;

namespace TraderEngine.Data.Entities;

public class AppUser : IdentityUser<Guid>
{
  public string DisplayName { get; set; } = string.Empty;

  /// <summary>
  /// Total number of successful logins for this user.
  /// </summary>
  public int LoginCount { get; set; }

  /// <summary>
  /// Date and time of the user's last successful login.
  /// </summary>
  public DateTimeOffset? LastLoginAt { get; set; }

  /// <summary>
  /// When true, the user is required to change their password on next login. Set for
  /// admin-created/admin-reset users; cleared once the user changes their password.
  /// </summary>
  public bool MustChangePassword { get; set; }
}
