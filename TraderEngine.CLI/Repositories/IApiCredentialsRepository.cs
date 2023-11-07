namespace TraderEngine.CLI.Repositories;

public interface IApiCredentialsRepository
{
  Task<ApiCredentials> GetApiCred(int userId, string exchangeName);
}

public class ApiCredentials
{
  public string Key { get; } = string.Empty;

  public string Secret { get; } = string.Empty;

  public ApiCredentials()
  {
  }

  public ApiCredentials(
    string key,
    string secret)
  {
    Key = key;
    Secret = secret;
  }
}