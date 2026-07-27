namespace TraderEngine.Data.Constants;

/// <summary>
/// Explicitly assignable application role names, shared by TraderEngine.API and TraderEngine.Web
/// since both authenticate against the same AppUser/IdentityRole store. There is deliberately no
/// "User" role: holding zero roles is itself the baseline tier — every authenticated account
/// already gets every endpoint that carries no explicit policy (see the FallbackPolicy in each
/// host's Program.cs), so a separate role for that would be redundant. <see cref="Admin"/> is the
/// only elevation on top of that baseline, granting user registration and role assignment.
/// </summary>
public static class Roles
{
  /// <summary>
  /// Full access, including user registration and role assignment.
  /// </summary>
  public const string Admin = "Admin";

  public static IReadOnlyList<string> All { get; } = [Admin];
}
