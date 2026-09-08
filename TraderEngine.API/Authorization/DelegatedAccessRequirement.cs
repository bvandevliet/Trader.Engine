using Microsoft.AspNetCore.Authorization;

namespace TraderEngine.API.Authorization;

/// <summary>
/// Marker requirement added to the default authorization policy (see Program.cs) so every
/// authenticated endpoint — current and future — independently re-verifies a delegated
/// (ActingAsClientId-carrying) token against the database, rather than relying on individual
/// controllers to remember to call IDelegationAuthorizationService themselves.
/// </summary>
public class DelegatedAccessRequirement : IAuthorizationRequirement
{
}