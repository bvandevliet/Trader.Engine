using System.ComponentModel.DataAnnotations;

namespace TraderEngine.Web.Models;

public class UserEditInput
{
  [Required(ErrorMessage = "Username is required.")]
  [Display(Name = "Username")]
  public string UserName { get; set; } = string.Empty;

  [Required(ErrorMessage = "Display name is required.")]
  [Display(Name = "Display name")]
  public string DisplayName { get; set; } = string.Empty;

  [Display(Name = "Roles (no role still grants standard access)")]
  public List<string> AssignedRoles { get; set; } = [];

  [StringLength(100, ErrorMessage = "{0} must be at least {2} characters long.", MinimumLength = 8)]
  [DataType(DataType.Password)]
  [Display(Name = "New password")]
  public string? NewPassword { get; set; }

  [DataType(DataType.Password)]
  [Display(Name = "Confirm new password")]
  [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
  public string? ConfirmPassword { get; set; }
}
