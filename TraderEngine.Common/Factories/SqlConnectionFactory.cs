using Dapper;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace TraderEngine.Common.Factories;

/// <summary>
/// Sql connection factory.
/// Lifetime of the connections is the same as the lifetime of the factory.
/// </summary>
public class SqlConnectionFactory : INamedTypeFactory<MySqlConnection>
{
  private readonly IConfiguration _configuration;
  private readonly Dictionary<string, MySqlConnection> _connections = new();

  public SqlConnectionFactory(IConfiguration config)
  {
    _configuration = config;
  }

  public MySqlConnection GetService(string name)
  {
    string? connectionString = _configuration.GetConnectionString(name);

    if (string.IsNullOrEmpty(connectionString))
    {
      throw new ArgumentException($"Connection string '{name}' not found in configuration.");
    }

    if (_connections.TryGetValue(name, out MySqlConnection? connection))
    {
      return connection;
    }
    else
    {
      connection = new MySqlConnection(connectionString);

      _connections.Add(name, connection);

      // Initialize the database.
      if (name == "MySql")
      {
        connection.Execute(
          "CREATE TABLE IF NOT EXISTS MarketCapData (\n" +
          "  id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,\n" +
          "  QuoteSymbol VARCHAR(12) NOT NULL,\n" +
          "  BaseSymbol VARCHAR(12) NOT NULL,\n" +
          "  Price VARCHAR(48) NOT NULL,\n" +
          "  MarketCap VARCHAR(48) NOT NULL,\n" +
          "  Tags TEXT,\n" +
          "  Updated DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP);");
      }

      return connection;
    }
  }
}
