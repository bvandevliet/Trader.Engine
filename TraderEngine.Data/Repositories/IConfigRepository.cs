using TraderEngine.Common.DTOs.API.Request;

namespace TraderEngine.Data.Repositories;

public interface IConfigRepository
{
  public Task<ConfigReqDto> GetConfig(Guid userId);

  public Task<IEnumerable<KeyValuePair<Guid, ConfigReqDto>>> GetConfigs();

  public Task<int> SaveConfig(Guid userId, ConfigReqDto configReqDto);
}
