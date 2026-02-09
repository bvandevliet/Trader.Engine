using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace TraderEngine.Common.Factories;

/// <summary>
/// Sql connection factory.
/// </summary>
public class SqlConnectionFactory : INamedTypeFactory<MySqlConnection>
{
  private readonly IConfiguration _configuration;

  public SqlConnectionFactory(IConfiguration config)
  {
    _configuration = config;
  }

  public MySqlConnection GetService(string name)
  {
    var connectionString = _configuration.GetConnectionString(name);

    if (string.IsNullOrEmpty(connectionString))
    {
      throw new ArgumentException($"Connection string for '{name}' not found in configuration.");
    }

    // Always create a new connection to be thread-safe.
    // The pool manager in the app domain takes care of pooling.
    return new MySqlConnection(connectionString);
  }
}
