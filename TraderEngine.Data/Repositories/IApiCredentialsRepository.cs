using TraderEngine.Common.DTOs.API.Request;

namespace TraderEngine.Data.Repositories;

/// <summary>
/// Whether exchange API credentials exist for a user, and when they were last saved — never the
/// key/secret itself, so callers can show a status indicator without decrypting anything.
/// </summary>
public record ApiCredentialStatus(DateTimeOffset UpdatedAt);

public interface IApiCredentialsRepository
{
  public Task<ApiCredReqDto> GetApiCred(Guid userId, string exchangeName);

  public Task<ApiCredentialStatus?> GetApiCredStatus(Guid userId, string exchangeName);

  public Task SaveApiCred(Guid userId, string exchangeName, ApiCredReqDto apiCred);
}
