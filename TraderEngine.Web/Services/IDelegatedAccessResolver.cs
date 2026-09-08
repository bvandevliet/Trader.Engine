using TraderEngine.Data.Entities;

namespace TraderEngine.Web.Services;

/// <summary>
/// <paramref name="EffectiveUser"/> is whose data a request is scoped to — <paramref name="Caller"/>
/// itself when not delegated. <paramref name="IsDelegated"/> is true only when
/// <paramref name="Caller"/> is a portfolio manager acting on <paramref name="EffectiveUser"/>'s
/// account via a verified grant.
/// </summary>
public record DelegatedAccessContext(AppUser Caller, AppUser EffectiveUser, bool IsDelegated);

/// <summary>
/// Resolves an <c>actingAsClientId</c> query parameter into whose account a request should act
/// on, re-verifying the delegation grant against the database on every call (the same check
/// TraderEngine.API's DelegatedAccessHandler independently re-runs server-side — this is the
/// Web-side half of that defense-in-depth pair, not a substitute for it).
/// </summary>
public interface IDelegatedAccessResolver
{
  /// <summary>
  /// Throws <see cref="DelegationAccessDeniedException"/> if <paramref name="actingAsClientId"/>
  /// is supplied but <paramref name="caller"/> has no currently active grant for it.
  /// </summary>
  Task<DelegatedAccessContext> ResolveAsync(AppUser caller, Guid? actingAsClientId);
}
