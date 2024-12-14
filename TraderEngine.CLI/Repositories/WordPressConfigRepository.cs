using AutoMapper;
using Dapper;
using Microsoft.Extensions.Options;
using MySqlConnector;
using TraderEngine.CLI.AppSettings;
using TraderEngine.CLI.DTOs.WordPress;
using TraderEngine.CLI.Helpers;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.Factories;

namespace TraderEngine.CLI.Repositories;

public class WordPressConfigRepository : IConfigRepository
{
  private readonly ILogger<WordPressConfigRepository> _logger;
  private readonly IMapper _mapper;
  private readonly INamedTypeFactory<MySqlConnection> _sqlConnectionFactory;
  private readonly CmsDbSettings _cmsDbSettings;

  public WordPressConfigRepository(
    ILogger<WordPressConfigRepository> logger,
    IMapper mapper,
    INamedTypeFactory<MySqlConnection> sqlConnectionFactory,
    IOptions<CmsDbSettings> cmsDbOptions)
  {
    _logger = logger;
    _mapper = mapper;
    _sqlConnectionFactory = sqlConnectionFactory;
    _cmsDbSettings = cmsDbOptions.Value;
  }

  private async Task<MySqlConnection> GetConnection()
  {
    var conn = _sqlConnectionFactory.GetService("CMS");

    await conn.OpenAsync();

    return conn;
  }

  public async Task<WordPressUserDto> GetUserInfo(int userId)
  {
    _logger.LogTrace("Getting user info for user '{UserId}' ..", userId);

    var sqlConn = await GetConnection();

    try
    {
      string sqlQuery = $@"
SELECT user_login, display_name, user_email
FROM {_cmsDbSettings.TablePrefix}users
WHERE ID = @UserId LIMIT 1;";

      var result = (await sqlConn.QueryFirstOrDefaultAsync<WordPressUserDto>(sqlQuery, new
      {
        UserId = userId
      }))!;

      return result;
    }
    finally
    {
      await sqlConn.CloseAsync();
    }
  }

  public async Task<ConfigReqDto> GetConfig(int userId)
  {
    _logger.LogTrace("Getting config for user '{UserId}' ..", userId);

    var sqlConn = await GetConnection();

    try
    {
      string sqlQuery = $@"
SELECT meta_value FROM {_cmsDbSettings.TablePrefix}usermeta
WHERE user_id = @UserId AND meta_key = 'trader_configuration'
LIMIT 1;";

      string dbConfig = (await sqlConn.QueryFirstOrDefaultAsync<string>(sqlQuery, new
      {
        UserId = userId
      }))!;

      var wpConfig = WordPressDbSerializer.Deserialize<WordPressConfigDto>(dbConfig);

      return _mapper.Map<ConfigReqDto>(wpConfig);
    }
    finally
    {
      await sqlConn.CloseAsync();
    }
  }

  public async Task<IEnumerable<KeyValuePair<int, ConfigReqDto>>> GetConfigs()
  {
    _logger.LogTrace("Getting configs for all users ..");

    var sqlConn = await GetConnection();

    try
    {
      string sqlQuery = $@"
SELECT user_id, meta_value
FROM {_cmsDbSettings.TablePrefix}usermeta
WHERE meta_key = 'trader_configuration';";

      var dbConfigs = await sqlConn.QueryAsync<(int user_id, string meta_value)>(sqlQuery);

      return dbConfigs
        .Select(dbConfig => new KeyValuePair<int, ConfigReqDto>(dbConfig.user_id,
        _mapper.Map<ConfigReqDto>(WordPressDbSerializer.Deserialize<WordPressConfigDto>(dbConfig.meta_value))));
    }
    finally
    {
      await sqlConn.CloseAsync();
    }
  }

  public async Task<int> SaveConfig(int userId, ConfigReqDto configReqDto)
  {
    _logger.LogTrace("Saving config for user '{UserId}' ..", userId);

    var wpConfig = _mapper.Map<WordPressConfigDto>(configReqDto);

    string dbConfig = WordPressDbSerializer.Serialize(wpConfig);

    var sqlConn = await GetConnection();

    try
    {
      string sqlQuery = $@"
UPDATE {_cmsDbSettings.TablePrefix}usermeta
SET meta_value = @MetaValue
WHERE user_id = @UserId AND meta_key = 'trader_configuration';";

      return await sqlConn.ExecuteAsync(sqlQuery, new
      {
        UserId = userId,
        MetaValue = dbConfig,
      });
    }
    finally
    {
      await sqlConn.CloseAsync();
    }
  }
}
