using Microsoft.EntityFrameworkCore;
using TraderEngine.API.Mappers;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Data;
using TraderEngine.Data.Entities;

namespace TraderEngine.API.Repositories;

public class EfConfigRepository : IConfigRepository
{
  private readonly TraderEngineDbContext _db;

  public EfConfigRepository(TraderEngineDbContext db)
  {
    _db = db;
  }

  public async Task<ConfigReqDto> GetConfig(Guid userId)
  {
    var entity = await _db.RebalancingConfigurations
      .AsNoTracking()
      .FirstOrDefaultAsync(c => c.UserId == userId);

    return entity == null ? new ConfigReqDto() : EfConfigMapper.MapConfig(entity);
  }

  public async Task<IEnumerable<KeyValuePair<Guid, ConfigReqDto>>> GetConfigs()
  {
    var entities = await _db.RebalancingConfigurations
      .AsNoTracking()
      .ToListAsync();

    return entities
      .Select(entity => new KeyValuePair<Guid, ConfigReqDto>(entity.UserId, EfConfigMapper.MapConfig(entity)));
  }

  public async Task<int> SaveConfig(Guid userId, ConfigReqDto configReqDto)
  {
    var entity = await _db.RebalancingConfigurations
      .FirstOrDefaultAsync(c => c.UserId == userId);

    if (entity == null)
    {
      entity = new RebalancingConfiguration { UserId = userId };
      _db.RebalancingConfigurations.Add(entity);
    }

    EfConfigMapper.MapConfigReverse(configReqDto, entity);

    return await _db.SaveChangesAsync();
  }
}
