using TraderEngine.Common.DTOs.API.Request;

namespace TraderEngine.API.Repositories;

public interface IApiCredentialsRepository
{
  public Task<ApiCredReqDto> GetApiCred(Guid userId, string exchangeName);

  public Task SaveApiCred(Guid userId, string exchangeName, ApiCredReqDto apiCred);
}
