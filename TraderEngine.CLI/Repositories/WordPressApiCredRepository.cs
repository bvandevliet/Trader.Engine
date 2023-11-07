using Dapper;
using Microsoft.Extensions.Options;
using MySqlConnector;
using TraderEngine.CLI.AppSettings;
using TraderEngine.CLI.Helpers;
using TraderEngine.Common.Factories;
using TraderEngine.Common.Services;

namespace TraderEngine.CLI.Repositories;

public class WordPressApiCredRepository : IApiCredentialsRepository
{
  private readonly ILogger<WordPressApiCredRepository> _logger;
  private readonly MySqlConnection _mySqlConnection;
  private readonly CmsDbSettings _cmsDbSettings;
  private readonly ICryptographyService _cryptographyService;

  public WordPressApiCredRepository(
    ILogger<WordPressApiCredRepository> logger,
    INamedTypeFactory<MySqlConnection> sqlConnectionFactory,
    IOptions<CmsDbSettings> cmsDbOptions,
    ICryptographyService cryptographyService)
  {
    _logger = logger;
    _mySqlConnection = sqlConnectionFactory.GetService("CMS");
    _cmsDbSettings = cmsDbOptions.Value;
    _cryptographyService = cryptographyService;
  }

  public async Task<ApiCredentials> GetApiCred(int userId, string exchangeName)
  {
    // Get encrypted API credentials from WordPress database.
    string userApiCred = await _mySqlConnection.QueryFirstOrDefaultAsync<string>(
      $"SELECT meta_value FROM {_cmsDbSettings.TablePrefix}usermeta\n" +
      "WHERE user_id = @UserId AND meta_key = @MetaKey\n" +
      "LIMIT 1;",
      new
      {
        UserId = userId,
        MetaKey = "api_keys",
      });

    var encryptedApiCred = WordPressDbSerializer.Deserialize<Dictionary<string, string>>(userApiCred);

    // Decrypt API credentials.
    if (null != encryptedApiCred &&
      encryptedApiCred.TryGetValue($"{exchangeName.ToLower()}_key", out string? encryptedApiKey) &&
      encryptedApiCred.TryGetValue($"{exchangeName.ToLower()}_secret", out string? encryptedApiSecret))
    {
      string apiKey = await _cryptographyService.Decrypt(encryptedApiKey);
      string apiSecret = await _cryptographyService.Decrypt(encryptedApiSecret);

      return new ApiCredentials(apiKey, apiSecret);
    }

    // Return empty API credentials if none found.
    return new ApiCredentials();
  }
}
