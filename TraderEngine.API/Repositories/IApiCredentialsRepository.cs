using TraderEngine.Common.DTOs.API.Request;

namespace TraderEngine.API.Repositories;

public interface IApiCredentialsRepository
{
  public Task<ApiCredReqDto> GetApiCred(int userId, string exchangeName);
}