using Microsoft.EntityFrameworkCore;
using TraderEngine.Data;
using TraderEngine.Data.Entities;
using TraderEngine.Data.Repositories;

namespace TraderEngine.Data.Tests.Repositories;

/// <summary>
/// Covers <see cref="EfPortfolioDelegationRepository"/>'s grant/revoke/re-grant semantics: one row
/// per (manager, client) pair reused across cycles, <c>RevokedAt</c> as the sole active/inactive
/// signal (see <see cref="PortfolioManagerGrant"/>'s doc comment). Uses EF Core's InMemory
/// provider — sufficient for this repository's own CRUD logic; the Postgres-only check constraint
/// (self-grant rejection) and cascade-delete behavior configured in
/// <see cref="TraderEngineDbContext.OnModelCreating"/> are relational-provider-only and are not,
/// and cannot be, exercised here.
/// </summary>
[TestClass]
public class EfPortfolioDelegationRepositoryTests
{
  private static TraderEngineDbContext NewDbContext()
  {
    var options = new DbContextOptionsBuilder<TraderEngineDbContext>()
      .UseInMemoryDatabase(Guid.NewGuid().ToString())
      .Options;

    return new TraderEngineDbContext(options);
  }

  private static AppUser NewUser(Guid id, string userName) => new()
  {
    Id = id,
    UserName = userName,
    NormalizedUserName = userName.ToUpperInvariant(),
    Email = $"{userName}@test.local",
    NormalizedEmail = $"{userName}@TEST.LOCAL",
    DisplayName = userName,
  };

  private static async Task<TraderEngineDbContext> SeededDbContext(params (Guid Id, string UserName)[] users)
  {
    var db = NewDbContext();

    db.Users.AddRange(users.Select(u => NewUser(u.Id, u.UserName)));

    await db.SaveChangesAsync();

    return db;
  }

  [TestMethod]
  public async Task HasActiveGrantAsync_NoGrantExists_ReturnsFalse()
  {
    // Arrange
    var managerId = Guid.NewGuid();
    var clientId = Guid.NewGuid();
    await using var db = await SeededDbContext((managerId, "manager"), (clientId, "client"));
    var repository = new EfPortfolioDelegationRepository(db);

    // Act
    var isAuthorized = await repository.HasActiveGrantAsync(managerId, clientId);

    // Assert
    Assert.IsFalse(isAuthorized);
  }

  [TestMethod]
  public async Task HasActiveGrantAsync_ActiveGrantExists_ReturnsTrue()
  {
    // Arrange
    var managerId = Guid.NewGuid();
    var clientId = Guid.NewGuid();
    await using var db = await SeededDbContext((managerId, "manager"), (clientId, "client"));
    var repository = new EfPortfolioDelegationRepository(db);

    await repository.GrantAsync(managerId, clientId);

    // Act
    var isAuthorized = await repository.HasActiveGrantAsync(managerId, clientId);

    // Assert
    Assert.IsTrue(isAuthorized);
  }

  [TestMethod]
  public async Task HasActiveGrantAsync_GrantWasRevoked_ReturnsFalse()
  {
    // Arrange
    var managerId = Guid.NewGuid();
    var clientId = Guid.NewGuid();
    await using var db = await SeededDbContext((managerId, "manager"), (clientId, "client"));
    var repository = new EfPortfolioDelegationRepository(db);

    await repository.GrantAsync(managerId, clientId);
    await repository.RevokeAsync(managerId, clientId);

    // Act
    var isAuthorized = await repository.HasActiveGrantAsync(managerId, clientId);

    // Assert
    Assert.IsFalse(isAuthorized);
  }

  /// <summary>
  /// A manager granted access to one client must never be reported as authorized for a different
  /// client purely because both pairs share the same manager — asserts the grant check is scoped
  /// to the exact pair, not just "this manager has some active grant somewhere."
  /// </summary>
  [TestMethod]
  public async Task HasActiveGrantAsync_ManagerGrantedForDifferentClient_ReturnsFalse()
  {
    // Arrange
    var managerId = Guid.NewGuid();
    var clientId = Guid.NewGuid();
    var otherClientId = Guid.NewGuid();
    await using var db = await SeededDbContext((managerId, "manager"), (clientId, "client"), (otherClientId, "other-client"));
    var repository = new EfPortfolioDelegationRepository(db);

    await repository.GrantAsync(managerId, otherClientId);

    // Act
    var isAuthorized = await repository.HasActiveGrantAsync(managerId, clientId);

    // Assert
    Assert.IsFalse(isAuthorized);
  }

  [TestMethod]
  public async Task GrantAsync_CalledRepeatedlyForSamePair_ReusesSingleRow()
  {
    // Arrange
    var managerId = Guid.NewGuid();
    var clientId = Guid.NewGuid();
    await using var db = await SeededDbContext((managerId, "manager"), (clientId, "client"));
    var repository = new EfPortfolioDelegationRepository(db);

    // Act
    await repository.GrantAsync(managerId, clientId);
    await repository.GrantAsync(managerId, clientId);
    await repository.GrantAsync(managerId, clientId);

    // Assert — never accumulates duplicate rows for the same pair, which the unique index also
    // enforces at the database level in production (Postgres), but this asserts the repository's
    // own upsert logic is what actually prevents it, not just the constraint as a safety net.
    var rowCount = await db.PortfolioManagerGrants.CountAsync(g => g.ManagerId == managerId && g.ClientId == clientId);
    Assert.AreEqual(1, rowCount);
  }

  [TestMethod]
  public async Task GrantAsync_AfterRevoke_ReactivatesTheSameRowInsteadOfInsertingANewOne()
  {
    // Arrange
    var managerId = Guid.NewGuid();
    var clientId = Guid.NewGuid();
    await using var db = await SeededDbContext((managerId, "manager"), (clientId, "client"));
    var repository = new EfPortfolioDelegationRepository(db);

    await repository.GrantAsync(managerId, clientId);
    var originalId = (await db.PortfolioManagerGrants.SingleAsync()).Id;

    await repository.RevokeAsync(managerId, clientId);

    // Act
    await repository.GrantAsync(managerId, clientId);

    // Assert
    var rows = await db.PortfolioManagerGrants.Where(g => g.ManagerId == managerId && g.ClientId == clientId).ToListAsync();
    Assert.AreEqual(1, rows.Count, "Re-granting after a revoke must reuse the existing row, not insert a second one.");
    Assert.AreEqual(originalId, rows[0].Id);
    Assert.IsNull(rows[0].RevokedAt);
  }

  [TestMethod]
  public async Task RevokeAsync_ActiveGrantExists_SetsRevokedAt()
  {
    // Arrange
    var managerId = Guid.NewGuid();
    var clientId = Guid.NewGuid();
    await using var db = await SeededDbContext((managerId, "manager"), (clientId, "client"));
    var repository = new EfPortfolioDelegationRepository(db);

    await repository.GrantAsync(managerId, clientId);

    // Act
    await repository.RevokeAsync(managerId, clientId);

    // Assert
    var grant = await db.PortfolioManagerGrants.SingleAsync();
    Assert.IsNotNull(grant.RevokedAt);
  }

  /// <summary>
  /// Hufter-proofing: revoking a pair that was never granted must be a silent no-op, not an
  /// exception (e.g. a client mass-clicking "revoke" links from stale/replayed page state) and
  /// must not fabricate a row that would then read back as ever having existed.
  /// </summary>
  [TestMethod]
  public async Task RevokeAsync_NoGrantEverExisted_DoesNotThrowOrCreateARow()
  {
    // Arrange
    var managerId = Guid.NewGuid();
    var clientId = Guid.NewGuid();
    await using var db = await SeededDbContext((managerId, "manager"), (clientId, "client"));
    var repository = new EfPortfolioDelegationRepository(db);

    // Act
    await repository.RevokeAsync(managerId, clientId);

    // Assert
    Assert.AreEqual(0, await db.PortfolioManagerGrants.CountAsync());
  }

  [TestMethod]
  public async Task RevokeAsync_AlreadyRevoked_IsNoOp()
  {
    // Arrange
    var managerId = Guid.NewGuid();
    var clientId = Guid.NewGuid();
    await using var db = await SeededDbContext((managerId, "manager"), (clientId, "client"));
    var repository = new EfPortfolioDelegationRepository(db);

    await repository.GrantAsync(managerId, clientId);
    await repository.RevokeAsync(managerId, clientId);
    var revokedAtFirstCall = (await db.PortfolioManagerGrants.SingleAsync()).RevokedAt;

    // Act
    await repository.RevokeAsync(managerId, clientId);

    // Assert — a second revoke must not throw and must not touch the already-set timestamp.
    var grant = await db.PortfolioManagerGrants.SingleAsync();
    Assert.AreEqual(revokedAtFirstCall, grant.RevokedAt);
  }

  [TestMethod]
  public async Task GetActiveManagersForClientAsync_ReturnsOnlyActiveGrantsForThatExactClient()
  {
    // Arrange
    var clientId = Guid.NewGuid();
    var activeManagerId = Guid.NewGuid();
    var revokedManagerId = Guid.NewGuid();
    var otherClientId = Guid.NewGuid();
    var managerOfOtherClientId = Guid.NewGuid();

    await using var db = await SeededDbContext(
      (clientId, "client"), (activeManagerId, "active-manager"), (revokedManagerId, "revoked-manager"),
      (otherClientId, "other-client"), (managerOfOtherClientId, "other-manager"));
    var repository = new EfPortfolioDelegationRepository(db);

    await repository.GrantAsync(activeManagerId, clientId);
    await repository.GrantAsync(revokedManagerId, clientId);
    await repository.RevokeAsync(revokedManagerId, clientId);
    await repository.GrantAsync(managerOfOtherClientId, otherClientId);

    // Act
    var managers = await repository.GetActiveManagersForClientAsync(clientId);

    // Assert
    Assert.AreEqual(1, managers.Count);
    Assert.AreEqual(activeManagerId, managers[0].UserId);
  }

  [TestMethod]
  public async Task GetActiveClientsForManagerAsync_ReturnsOnlyActiveGrantsForThatExactManager()
  {
    // Arrange
    var managerId = Guid.NewGuid();
    var activeClientId = Guid.NewGuid();
    var revokedClientId = Guid.NewGuid();
    var otherManagerId = Guid.NewGuid();
    var clientOfOtherManagerId = Guid.NewGuid();

    await using var db = await SeededDbContext(
      (managerId, "manager"), (activeClientId, "active-client"), (revokedClientId, "revoked-client"),
      (otherManagerId, "other-manager"), (clientOfOtherManagerId, "other-client"));
    var repository = new EfPortfolioDelegationRepository(db);

    await repository.GrantAsync(managerId, activeClientId);
    await repository.GrantAsync(managerId, revokedClientId);
    await repository.RevokeAsync(managerId, revokedClientId);
    await repository.GrantAsync(otherManagerId, clientOfOtherManagerId);

    // Act
    var clients = await repository.GetActiveClientsForManagerAsync(managerId);

    // Assert
    Assert.AreEqual(1, clients.Count);
    Assert.AreEqual(activeClientId, clients[0].UserId);
  }
}
