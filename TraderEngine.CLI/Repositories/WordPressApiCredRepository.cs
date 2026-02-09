using Dapper;
using Microsoft.Extensions.Options;
using MySqlConnector;
using TraderEngine.CLI.AppSettings;
using TraderEngine.CLI.Helpers;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.Factories;
using TraderEngine.Common.Services;

namespace TraderEngine.CLI.Repositories;

public class WordPressApiCredRepository : IApiCredentialsRepository
{
  private readonly ILogger<WordPressApiCredRepository> _logger;
  private readonly INamedTypeFactory<MySqlConnection> _sqlConnectionFactory;
  private readonly CmsDbSettings _cmsDbSettings;
  private readonly ICryptographyService _cryptographyService;

  public WordPressApiCredRepository(
    ILogger<WordPressApiCredRepository> logger,
    INamedTypeFactory<MySqlConnection> sqlConnectionFactory,
    IOptions<CmsDbSettings> cmsDbOptions,
    ICryptographyService cryptographyService)
  {
    _logger = logger;
    _sqlConnectionFactory = sqlConnectionFactory;
    _cmsDbSettings = cmsDbOptions.Value;
    _cryptographyService = cryptographyService;
  }

  private async Task<MySqlConnection> GetConnection()
  {
    var conn = _sqlConnectionFactory.GetService("CMS");

    await conn!.OpenAsync();

    return conn;
  }

  public async Task<ApiCredReqDto> GetApiCred(int userId, string exchangeName)
  {
    var sqlConn = await GetConnection();

    string userApiCred;
    try
    {
      var sqlQuery = $@"
SELECT meta_value FROM {_cmsDbSettings.TablePrefix}usermeta
WHERE user_id = @UserId AND meta_key = 'api_keys' LIMIT 1;";

      // Get encrypted API credentials from WordPress database.
      userApiCred = (await sqlConn.QueryFirstOrDefaultAsync<string>(sqlQuery, new
      {
        UserId = userId,
      }))!;
    }
    finally
    {
      await sqlConn.CloseAsync();
    }

    var encryptedApiCred = WordPressDbSerializer.Deserialize<Dictionary<string, string>>(userApiCred);

    // Decrypt API credentials.
    if (null != encryptedApiCred &&
      encryptedApiCred.TryGetValue($"{exchangeName.ToLower()}_key", out var encryptedApiKey) &&
      encryptedApiCred.TryGetValue($"{exchangeName.ToLower()}_secret", out var encryptedApiSecret))
    {
      var apiKey = await _cryptographyService.Decrypt(encryptedApiKey);
      var apiSecret = await _cryptographyService.Decrypt(encryptedApiSecret);

      return new ApiCredReqDto()
      {
        ApiKey = apiKey,
        ApiSecret = apiSecret,
      };
    }

    // Return empty API credentials if none found.
    return new ApiCredReqDto()
    {
      ApiKey = string.Empty,
      ApiSecret = string.Empty,
    };
  }
}
