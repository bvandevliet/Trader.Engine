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
  private readonly MySqlConnection _mySqlConnection;
  private readonly CmsDbSettings _cmsDbSettings;

  public WordPressConfigRepository(
    ILogger<WordPressConfigRepository> logger,
    IMapper mapper,
    INamedTypeFactory<MySqlConnection> sqlConnectionFactory,
    IOptions<CmsDbSettings> cmsDbOptions)
  {
    _logger = logger;
    _mapper = mapper;
    _mySqlConnection = sqlConnectionFactory.GetService("CMS");
    _cmsDbSettings = cmsDbOptions.Value;
  }
  public async Task<ConfigReqDto> GetConfig(int userId)
  {
    string dbConfig = await _mySqlConnection.QueryFirstOrDefaultAsync<string>(
      $"SELECT meta_value FROM {_cmsDbSettings.TablePrefix}usermeta\n" +
      "WHERE user_id = @UserId AND meta_key = 'trader_configuration'\n" +
      "LIMIT 1;",
      new { UserId = userId });

    var wpConfig = WordPressDbSerializer.Deserialize<WordPressConfigDto>(dbConfig);

    return _mapper.Map<ConfigReqDto>(wpConfig);
  }

  public async Task<IEnumerable<KeyValuePair<int, ConfigReqDto>>> GetConfigs()
  {
    var dbConfigs = await _mySqlConnection.QueryAsync<KeyValuePair<int, string>>(
      $"SELECT user_id, meta_value FROM {_cmsDbSettings.TablePrefix}usermeta\n" +
      "WHERE meta_key = 'trader_configuration';");

    return dbConfigs
      .Select(dbConfig => new KeyValuePair<int, ConfigReqDto>(dbConfig.Key,
      _mapper.Map<ConfigReqDto>(WordPressDbSerializer.Deserialize<WordPressConfigDto>(dbConfig.Value))));
  }

  public Task SaveConfig(int userId, ConfigReqDto configReqDto)
  {
    var wpConfig = _mapper.Map<WordPressConfigDto>(configReqDto);

    string dbConfig = WordPressDbSerializer.Serialize(wpConfig);

    return _mySqlConnection.ExecuteAsync(
      $"UPDATE {_cmsDbSettings.TablePrefix}usermeta\n" +
      "SET meta_value = @MetaValue\n" +
      "WHERE user_id = @UserId AND meta_key = @MetaKey;",
      new
      {
        UserId = userId,
        MetaKey = "trader_configuration",
        MetaValue = dbConfig,
      });
  }
}
