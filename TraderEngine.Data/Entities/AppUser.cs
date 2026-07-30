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

  /// <summary>
  /// IANA (Linux/macOS) or Windows time zone id used to localize timestamps in contexts with no
  /// client-side JS to do it, e.g. automation emails (see EmailNotificationService). Defaults to
  /// whatever zone the server itself is running in at account-creation time — a reasonable guess
  /// until the user overrides it on their profile page, and the only guess the server can make
  /// without asking the browser.
  /// </summary>
  public string TimeZoneId { get; set; } = TimeZoneInfo.Local.Id;
}
