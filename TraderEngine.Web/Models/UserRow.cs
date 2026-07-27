using System.ComponentModel.DataAnnotations;

namespace TraderEngine.Web.Models;

public class UserRow
{
  public Guid Id { get; set; }

  [Display(Name = "Username")]
  public string UserName { get; set; } = string.Empty;

  [Display(Name = "Email")]
  public string Email { get; set; } = string.Empty;

  [Display(Name = "Display name")]
  public string DisplayName { get; set; } = string.Empty;

  [Display(Name = "Roles")]
  public IReadOnlyList<string> Roles { get; set; } = [];

  public bool LockedOut { get; set; }

  [Display(Name = "# Logins")]
  public int LoginCount { get; set; }

  [Display(Name = "Last login")]
  public DateTimeOffset? LastLoginAt { get; set; }

  public bool MustChangePassword { get; set; }
}
