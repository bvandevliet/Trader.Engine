namespace TraderEngine.Data.Entities;

/// <summary>
/// A client's delegation of portfolio access to a portfolio manager. One row per
/// (<see cref="ManagerId"/>, <see cref="ClientId"/>) pair, ever — granting after a prior revoke
/// reuses the same row (<see cref="RevokedAt"/> cleared, <see cref="GrantedAt"/> refreshed) rather
/// than inserting a new one. <see cref="RevokedAt"/> is the single source of truth for whether the
/// grant is currently active (null = active) — there is deliberately no separate stored bool, to
/// avoid a flag that could drift out of sync with the timestamp.
/// </summary>
public class PortfolioManagerGrant
{
  public Guid Id { get; set; }

  /// <summary>
  /// The portfolio-manager-role user this grant lets act on <see cref="ClientId"/>'s account.
  /// </summary>
  public Guid ManagerId { get; set; }

  public AppUser Manager { get; set; } = null!;

  /// <summary>
  /// The user who owns the portfolio and granted access to <see cref="ManagerId"/>.
  /// </summary>
  public Guid ClientId { get; set; }

  public AppUser Client { get; set; } = null!;

  public DateTimeOffset GrantedAt { get; set; }

  public DateTimeOffset? RevokedAt { get; set; }
}
