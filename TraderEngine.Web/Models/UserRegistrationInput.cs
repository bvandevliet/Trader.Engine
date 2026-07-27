using System.ComponentModel.DataAnnotations;

namespace TraderEngine.Web.Models;

public class UserRegistrationInput
{
  [Required]
  [StringLength(50, ErrorMessage = "{0} must be between {2} and {1} characters long.", MinimumLength = 3)]
  [Display(Name = "Username")]
  public string UserName { get; set; } = string.Empty;

  [Required]
  [EmailAddress]
  [Display(Name = "Email")]
  public string Email { get; set; } = string.Empty;

  [Required]
  [StringLength(100, ErrorMessage = "{0} must be between {2} and {1} characters long.", MinimumLength = 1)]
  [Display(Name = "Display name")]
  public string DisplayName { get; set; } = string.Empty;

  [Required]
  [StringLength(100, ErrorMessage = "{0} must be at least {2} characters long.", MinimumLength = 8)]
  [DataType(DataType.Password)]
  [Display(Name = "Password")]
  public string Password { get; set; } = string.Empty;

  [DataType(DataType.Password)]
  [Display(Name = "Confirm password")]
  [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
  public string ConfirmPassword { get; set; } = string.Empty;

  [Display(Name = "Roles")]
  public List<string> AssignedRoles { get; set; } = [];
}
