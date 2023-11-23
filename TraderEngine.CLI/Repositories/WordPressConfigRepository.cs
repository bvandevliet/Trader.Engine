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

  private MySqlConnection GetConnection() => _sqlConnectionFactory.GetService("CMS");

  public async Task<WordPressUserDto> GetUserInfo(int userId)
  {
    var sqlConn = GetConnection();

    var result = (await sqlConn.QueryFirstOrDefaultAsync<WordPressUserDto>(
      $"SELECT user_login, display_name, user_email FROM {_cmsDbSettings.TablePrefix}users\n" +
      "WHERE ID = @UserId LIMIT 1;", new { UserId = userId }))!;

    await sqlConn.CloseAsync();

    return result;
  }

  public async Task<ConfigReqDto> GetConfig(int userId)
  {
    var sqlConn = GetConnection();

    string dbConfig = (await sqlConn.QueryFirstOrDefaultAsync<string>(
      $"SELECT meta_value FROM {_cmsDbSettings.TablePrefix}usermeta\n" +
      "WHERE user_id = @UserId AND meta_key = 'trader_configuration'\n" +
      "LIMIT 1;", new { UserId = userId }))!;

    await sqlConn.CloseAsync();

    var wpConfig = WordPressDbSerializer.Deserialize<WordPressConfigDto>(dbConfig);

    return _mapper.Map<ConfigReqDto>(wpConfig);
  }

  public async Task<IEnumerable<KeyValuePair<int, ConfigReqDto>>> GetConfigs()
  {
    var sqlConn = GetConnection();

    var dbConfigs = await sqlConn.QueryAsync<(int user_id, string meta_value)>(
      $"SELECT user_id, meta_value FROM {_cmsDbSettings.TablePrefix}usermeta\n" +
      "WHERE meta_key = 'trader_configuration';");

    await sqlConn.CloseAsync();

    return dbConfigs
      .Select(dbConfig => new KeyValuePair<int, ConfigReqDto>(dbConfig.user_id,
      _mapper.Map<ConfigReqDto>(WordPressDbSerializer.Deserialize<WordPressConfigDto>(dbConfig.meta_value))));
  }

  public async Task<int> SaveConfig(int userId, ConfigReqDto configReqDto)
  {
    var wpConfig = _mapper.Map<WordPressConfigDto>(configReqDto);

    string dbConfig = WordPressDbSerializer.Serialize(wpConfig);

    var sqlConn = GetConnection();

    var result = await sqlConn.ExecuteAsync(
      $"UPDATE {_cmsDbSettings.TablePrefix}usermeta\n" +
      "SET meta_value = @MetaValue\n" +
      "WHERE user_id = @UserId AND meta_key = @MetaKey;",
      new
      {
        UserId = userId,
        MetaKey = "trader_configuration",
        MetaValue = dbConfig,
      });

    await sqlConn.CloseAsync();

    return result;
  }
}
