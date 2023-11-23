using System.ComponentModel.DataAnnotations;

namespace TraderEngine.CLI.DTOs.WordPress;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>")]
public class WordPressUserDto
{
  public string user_login { get; set; } = null!;

  public string display_name { get; set; } = null!;

  [EmailAddress]
  public string user_email { get; set; } = null!;
}
