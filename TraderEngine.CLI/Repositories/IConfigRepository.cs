using TraderEngine.Common.DTOs.API.Request;

namespace TraderEngine.CLI.Repositories;

public interface IConfigRepository
{
  public Task<ConfigReqDto> GetConfig(int userId);

  public Task<IEnumerable<KeyValuePair<int, ConfigReqDto>>> GetConfigs();

  public Task SaveConfig(int userId, ConfigReqDto configReqDto);
}
