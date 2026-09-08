using Microsoft.EntityFrameworkCore;
using TraderEngine.Data.Entities;

namespace TraderEngine.Data.Repositories;

public class EfPortfolioDelegationRepository : IPortfolioDelegationRepository
{
  private readonly TraderEngineDbContext _db;

  public EfPortfolioDelegationRepository(TraderEngineDbContext db)
  {
    _db = db;
  }

  public async Task<bool> HasActiveGrantAsync(Guid managerId, Guid clientId)
  {
    return await _db.PortfolioManagerGrants.AsNoTracking().AnyAsync(g =>
      g.ManagerId == managerId && g.ClientId == clientId && g.RevokedAt == null);
  }

  public async Task GrantAsync(Guid managerId, Guid clientId)
  {
    var entity = await _db.PortfolioManagerGrants
      .FirstOrDefaultAsync(g => g.ManagerId == managerId && g.ClientId == clientId);

    if (entity == null)
    {
      entity = new PortfolioManagerGrant { ManagerId = managerId, ClientId = clientId };
      _db.PortfolioManagerGrants.Add(entity);
    }

    entity.GrantedAt = DateTimeOffset.UtcNow;
    entity.RevokedAt = null;

    await _db.SaveChangesAsync();
  }

  public async Task RevokeAsync(Guid managerId, Guid clientId)
  {
    var entity = await _db.PortfolioManagerGrants.FirstOrDefaultAsync(g =>
      g.ManagerId == managerId && g.ClientId == clientId && g.RevokedAt == null);

    if (entity == null)
      return;

    entity.RevokedAt = DateTimeOffset.UtcNow;

    await _db.SaveChangesAsync();
  }

  public async Task<List<DelegationCounterpartRow>> GetActiveManagersForClientAsync(Guid clientId)
  {
    return await _db.PortfolioManagerGrants
      .AsNoTracking()
      .Where(g => g.ClientId == clientId && g.RevokedAt == null)
      .OrderBy(g => g.Manager.UserName)
      .Select(g => new DelegationCounterpartRow(g.Manager.Id, g.Manager.UserName ?? string.Empty, g.Manager.DisplayName, g.GrantedAt))
      .ToListAsync();
  }

  public async Task<List<DelegationCounterpartRow>> GetActiveClientsForManagerAsync(Guid managerId)
  {
    return await _db.PortfolioManagerGrants
      .AsNoTracking()
      .Where(g => g.ManagerId == managerId && g.RevokedAt == null)
      .OrderBy(g => g.Client.UserName)
      .Select(g => new DelegationCounterpartRow(g.Client.Id, g.Client.UserName ?? string.Empty, g.Client.DisplayName, g.GrantedAt))
      .ToListAsync();
  }
}
