using TraderEngine.API.DTOs.WordPress;
using TraderEngine.Common.DTOs.API.Request;

namespace TraderEngine.API.Repositories;

public interface IConfigRepository
{
  public Task<WordPressUserDto> GetUserInfo(int userId);

  public Task<ConfigReqDto> GetConfig(int userId);

  public Task<IEnumerable<KeyValuePair<int, ConfigReqDto>>> GetConfigs();

  public Task<int> SaveConfig(int userId, ConfigReqDto configReqDto);
}
