namespace TraderEngine.Data.Constants;

/// <summary>
/// Authorization policy names, shared by TraderEngine.API and TraderEngine.Web.
/// </summary>
public static class Policies
{
  /// <summary>
  /// Restricts access to users in the <see cref="Roles.Admin"/> role only.
  /// </summary>
  public const string AdminOnly = "AdminOnly";
}
