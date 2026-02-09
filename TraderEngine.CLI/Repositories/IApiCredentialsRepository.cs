using TraderEngine.Common.DTOs.API.Request;

namespace TraderEngine.CLI.Repositories;

public interface IApiCredentialsRepository
{
  public Task<ApiCredReqDto> GetApiCred(int userId, string exchangeName);
}