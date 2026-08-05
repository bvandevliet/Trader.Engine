namespace TraderEngine.API.AppSettings;

/// <summary>
/// Bootstraps the single operator account this app is designed for. Left empty (the default),
/// no user is seeded — set all three to create one idempotently on startup. Intended as a
/// stopgap until Phase 5 ships proper account management; not a general registration mechanism.
/// </summary>
public class AdminSeedSettings
{
  public string UserName { get; set; } = string.Empty;

  public string Email { get; set; } = string.Empty;

  public string Password { get; set; } = string.Empty;
}
