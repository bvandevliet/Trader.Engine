using Dapper;
using MySqlConnector;

namespace TraderEngine.Common.Bootstrap;
public static class SqlDatabase
{
  public static async Task Initialize(this MySqlConnection mySqlConnection)
  {
    await mySqlConnection.ExecuteAsync(
      "CREATE TABLE IF NOT EXISTS MarketCapData (\n" +
      "  id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,\n" +
      "  QuoteSymbol VARCHAR(12) NOT NULL,\n" +
      "  BaseSymbol VARCHAR(12) NOT NULL,\n" +
      "  Price VARCHAR(48) NOT NULL,\n" +
      "  MarketCap VARCHAR(48) NOT NULL,\n" +
      "  Tags TEXT,\n" +
      "  Updated DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP);");
  }
}