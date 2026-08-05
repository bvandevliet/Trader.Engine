using Dapper;
using Microsoft.Extensions.Options;
using MySqlConnector;
using TraderEngine.Migration.AppSettings;

namespace TraderEngine.Migration.WordPress;

/// <summary>
/// Read-only access to the legacy WordPress/MariaDB store — <c>wp_users</c> and
/// <c>wp_usermeta</c> — used exclusively as a one-time source for <see cref="Program"/>'s
/// migration to the new Postgres-backed store. Never writes back to WordPress.
/// </summary>
public class WordPressReader
{
  private readonly string _connectionString;
  private readonly WordPressSettings _settings;

  public WordPressReader(string connectionString, IOptions<WordPressSettings> settings)
  {
    _connectionString = connectionString;
    _settings = settings.Value;
  }

  private async Task<MySqlConnection> GetConnection()
  {
    var conn = new MySqlConnection(_connectionString);

    await conn.OpenAsync();

    return conn;
  }

  /// <summary>
  /// All WordPress user accounts, keyed by their WordPress <c>ID</c>.
  /// </summary>
  public async Task<IReadOnlyDictionary<int, WordPressUserDto>> GetAllUsers()
  {
    await using var sqlConn = await GetConnection();

    var sqlQuery = $@"
SELECT ID, user_login, display_name, user_email
FROM {_settings.TablePrefix}users;";

    var rows = await sqlConn.QueryAsync<(int ID, string user_login, string display_name, string user_email)>(sqlQuery);

    return rows.ToDictionary(row => row.ID, row => new WordPressUserDto
    {
      user_login = row.user_login,
      display_name = row.display_name,
      user_email = row.user_email,
    });
  }

  /// <summary>
  /// Every user's rebalancing configuration blob, keyed by WordPress user <c>ID</c>. Skips users
  /// who never saved a configuration (no <c>trader_configuration</c> usermeta row).
  /// </summary>
  public async Task<IReadOnlyDictionary<int, WordPressConfigDto>> GetAllConfigs()
  {
    await using var sqlConn = await GetConnection();

    var sqlQuery = $@"
SELECT user_id, meta_value
FROM {_settings.TablePrefix}usermeta
WHERE meta_key = 'trader_configuration';";

    var rows = await sqlConn.QueryAsync<(int user_id, string meta_value)>(sqlQuery);

    return rows.ToDictionary(
      row => row.user_id,
      row => WordPressDbSerializer.Deserialize<WordPressConfigDto>(row.meta_value)!);
  }

  /// <summary>
  /// Every user's still-encrypted exchange API key/secret pairs (e.g. <c>bitvavo_key</c>,
  /// <c>bitvavo_secret</c>), keyed by WordPress user <c>ID</c>. Ciphertext is only decryptable by
  /// the legacy cryptography service (see <see cref="Services.CryptographyClient"/>) — never by
  /// this process directly.
  /// </summary>
  public async Task<IReadOnlyDictionary<int, Dictionary<string, string>>> GetAllEncryptedApiKeys()
  {
    await using var sqlConn = await GetConnection();

    var sqlQuery = $@"
SELECT user_id, meta_value
FROM {_settings.TablePrefix}usermeta
WHERE meta_key = 'api_keys';";

    var rows = await sqlConn.QueryAsync<(int user_id, string meta_value)>(sqlQuery);

    return rows.ToDictionary(
      row => row.user_id,
      row => WordPressDbSerializer.Deserialize<Dictionary<string, string>>(row.meta_value) ?? []);
  }
}
