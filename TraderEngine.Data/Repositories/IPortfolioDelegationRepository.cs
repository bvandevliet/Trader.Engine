namespace TraderEngine.Data.Repositories;

/// <summary>
/// A manager or client row for the Delegation page's lists — just enough of the counterpart
/// <see cref="Entities.AppUser"/> to render it, plus when the grant was (re-)established.
/// </summary>
public record DelegationCounterpartRow(Guid UserId, string UserName, string DisplayName, DateTimeOffset GrantedAt);

public interface IPortfolioDelegationRepository
{
  /// <summary>
  /// True only when an active (non-revoked) grant exists for this exact pair.
  /// </summary>
  Task<bool> HasActiveGrantAsync(Guid managerId, Guid clientId);

  /// <summary>
  /// Grants access, or re-activates a previously revoked grant for the same pair. Idempotent:
  /// granting an already-active pair just refreshes <c>GrantedAt</c>.
  /// </summary>
  Task GrantAsync(Guid managerId, Guid clientId);

  /// <summary>
  /// Revokes the grant for this exact pair, if one is currently active. No-op if none exists or
  /// it's already revoked.
  /// </summary>
  Task RevokeAsync(Guid managerId, Guid clientId);

  /// <summary>
  /// Managers currently granted access to <paramref name="clientId"/>'s portfolio.
  /// </summary>
  Task<List<DelegationCounterpartRow>> GetActiveManagersForClientAsync(Guid clientId);

  /// <summary>
  /// Clients who currently grant <paramref name="managerId"/> access to their portfolio.
  /// </summary>
  Task<List<DelegationCounterpartRow>> GetActiveClientsForManagerAsync(Guid managerId);
}
