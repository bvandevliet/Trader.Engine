using Dapper;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace TraderEngine.Common.Factories;

/// <summary>
/// Sql connection factory.
/// Lifetime of the connections is the same as the lifetime of the factory.
/// </summary>
public class SqlConnectionFactory : INamedTypeFactory<MySqlConnection>, IDisposable
{
  private readonly IConfiguration _configuration;
  private readonly List<KeyValuePair<string, MySqlConnection>> _connections = new();
  private bool _disposedValue;

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

    // Always create a new connection to be thread-safe.
    var connection = new MySqlConnection(connectionString);

    // Initialize the database.
    if (name == "MySql" && !_connections.Any(conn => conn.Key == name))
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

    _connections.Add(new(name, connection));

    return connection;
  }

  protected virtual void Dispose(bool disposing)
  {
    if (!_disposedValue)
    {
      if (disposing)
      {
        _connections.ForEach(conn => conn.Value.Dispose());
      }

      _disposedValue = true;
    }
  }

  public void Dispose()
  {
    // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method.
    Dispose(disposing: true);
    GC.SuppressFinalize(this);
  }
}
