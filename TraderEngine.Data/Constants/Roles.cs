namespace TraderEngine.Data.Constants;

/// <summary>
/// Explicitly assignable application role names, shared by TraderEngine.API and TraderEngine.Web
/// since both authenticate against the same AppUser/IdentityRole store. There is deliberately no
/// "User" role: holding zero roles is itself the baseline tier — every authenticated account
/// already gets every endpoint that carries no explicit policy (see the FallbackPolicy in each
/// host's Program.cs), so a separate role for that would be redundant. <see cref="Admin"/> is an
/// elevation on top of that baseline, granting user registration and role assignment.
/// <see cref="PortfolioManager"/> is a self-service, non-elevating opt-in instead — it grants no
/// access on its own, only eligibility to act on a specific client's portfolio once that client
/// has separately, unilaterally granted them a <c>PortfolioManagerGrant</c>.
/// </summary>
public static class Roles
{
  /// <summary>
  /// Full access, including user registration and role assignment.
  /// </summary>
  public const string Admin = "Admin";

  /// <summary>
  /// Eligible to be granted delegated access to another user's portfolio. Self-service — any
  /// authenticated user may opt in or out immediately, no approval required. Holding this role
  /// grants no access by itself; see <c>PortfolioManagerGrant</c>.
  /// </summary>
  public const string PortfolioManager = "PortfolioManager";

  public static IReadOnlyList<string> All { get; } = [Admin, PortfolioManager];
}