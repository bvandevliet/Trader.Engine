using AutoMapper;
using Dapper;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using TraderEngine.Common.Abstracts;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.DTOs.Database;
using TraderEngine.Common.Factories;

namespace TraderEngine.Common.Repositories;

public class MarketCapInternalRepository : MarketCapHandlingBase, IMarketCapInternalRepository
{
  private readonly ILogger<MarketCapInternalRepository> _logger;
  private readonly IMapper _mapper;
  private readonly INamedTypeFactory<MySqlConnection> _sqlConnectionFactory;

  public MarketCapInternalRepository(
    ILogger<MarketCapInternalRepository> logger,
    IMapper mapper,
    INamedTypeFactory<MySqlConnection> sqlConnectionFactory)
  {
    _logger = logger;
    _mapper = mapper;
    _sqlConnectionFactory = sqlConnectionFactory;
  }

  private async Task<MySqlConnection> GetConnection()
  {
    var conn = _sqlConnectionFactory.GetService("MySql");

    await conn!.OpenAsync();

    return conn;
  }

  public async Task<int> InitDatabase()
  {
    var sqlConn = await GetConnection();

    try
    {
      return await sqlConn.ExecuteAsync(@"
CREATE TABLE IF NOT EXISTS MarketCapData (
  id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  QuoteSymbol VARCHAR(12) NOT NULL,
  BaseSymbol VARCHAR(12) NOT NULL,
  Price VARCHAR(48) NOT NULL,
  MarketCap VARCHAR(48) NOT NULL,
  Tags TEXT,
  Updated DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP );");
    }
    finally
    {
      await sqlConn.CloseAsync();
    }
  }

  public async Task<int> CleanupDatabase(int daysRetention = 14)
  {
    _logger.LogDebug("Cleaning up market cap database ..");

    var sqlConn = await GetConnection();

    try
    {
      int rowsAffected = await sqlConn.ExecuteAsync(@"
DELETE FROM MarketCapData
WHERE Updated < @RetentionDate;", new
      {
        RetentionDate = DateTime.UtcNow.AddDays(-daysRetention)
      });

      _logger.LogInformation("Cleaned up {rows} stale records from market cap database table.", rowsAffected);

      return rowsAffected;
    }
    finally
    {
      await sqlConn.CloseAsync();
    }
  }

  public async Task<int> TryInsert(MarketCapDataDto marketCap)
  {
    _logger.LogTrace("Inserting market cap record of '{market}' to database ..", marketCap.Market);

    if (!IsCloseToTheWholeHour(marketCap.Updated))
    {
      _logger.LogWarning("Updated time '{updated}' of market cap of '{market}' is not close to the whole hour.",
        marketCap.Updated, marketCap.Market);
    }

    var sqlConn = await GetConnection();

    try
    {
      string sqlSelect = @"
SELECT * FROM MarketCapData
WHERE QuoteSymbol = @QuoteSymbol AND BaseSymbol = @BaseSymbol
ORDER BY Updated DESC LIMIT 1;";

      var lastRecord = await sqlConn.QueryFirstOrDefaultAsync<MarketCapDataDb>(sqlSelect, new
      {
        marketCap.Market.QuoteSymbol,
        marketCap.Market.BaseSymbol
      });

      int rowsAffected = 0;

      if (null != lastRecord && OffsetMinutes(marketCap.Updated, lastRecord.Updated) + laterTolerance < 60 - earlierTolerance)
      {
        _logger.LogWarning("Updated time '{updated}' of market cap of '{market}' is too close to the previous record.",
          marketCap.Updated, marketCap.Market);

        string sqlDelete = @"
DELETE FROM MarketCapData
WHERE QuoteSymbol = @QuoteSymbol AND BaseSymbol = @BaseSymbol;";

        int rowsDeleted = await sqlConn.ExecuteAsync(sqlDelete, new
        {
          marketCap.Market.QuoteSymbol,
          marketCap.Market.BaseSymbol
        });

        _logger.LogTrace("Deleted '{rows}' old records of market cap of '{market}' from database.",
          rowsDeleted, marketCap.Market);
      }

      _logger.LogTrace("Inserting new market cap record of '{market}' to database ..", marketCap.Market);

      string sqlInsert = @"
INSERT INTO MarketCapData ( QuoteSymbol, BaseSymbol, Price, MarketCap, Tags, Updated )
VALUES ( @QuoteSymbol, @BaseSymbol, @Price, @MarketCap, @Tags, @Updated );";

      var marketCapData = _mapper.Map<MarketCapDataDb>(marketCap);

      rowsAffected += await sqlConn.ExecuteAsync(sqlInsert, marketCapData);

      if (0 == rowsAffected)
      {
        _logger.LogError("Failed to insert market cap of '{market}' to database.", marketCap.Market);
      }
      else
      {
        _logger.LogTrace("Inserted market cap of '{market}' to database.", marketCap.Market);
      }

      return rowsAffected;
    }
    finally
    {
      await sqlConn.CloseAsync();
    }
  }

  public async Task<int> TryInsertMany(IEnumerable<MarketCapDataDto> marketCaps)
  {
    _logger.LogDebug("Inserting market cap records into database ..");

    int rowsAffected = 0;

    // Insert in chunks to avoid overloading the connection pool and cause timeouts.
    // Chunk size should ideally be equeal to the pool size.
    foreach (var batch in marketCaps.Chunk(8))
    {
      rowsAffected += (await Task.WhenAll(batch.Select(TryInsert))).Sum();
    }

    _logger.LogInformation("Inserted {rows} market cap records into database.", rowsAffected);

    return rowsAffected;
  }

  public async Task<IEnumerable<MarketCapDataDto>> ListHistorical(MarketReqDto market, int hours = 24)
  {
    _logger.LogTrace("Listing historical market cap for '{market}' ..", market);

    var sqlConn = await GetConnection();

    try
    {
      string sqlQuery = @"
SELECT * FROM MarketCapData
WHERE
  QuoteSymbol = @QuoteSymbol
  AND BaseSymbol = @BaseSymbol
  AND Updated >= @Updated
ORDER BY Updated DESC;";

      var listHistorical = await sqlConn.QueryAsync<MarketCapDataDb>(sqlQuery, new
      {
        market.QuoteSymbol,
        market.BaseSymbol,
        Updated = DateTime.UtcNow.AddHours(-(hours + earlierTolerance / 60)),
      });

      return _mapper.Map<IEnumerable<MarketCapDataDto>>(listHistorical);
    }
    finally
    {
      await sqlConn.CloseAsync();
    }
  }

  // TODO: CACHE RECENT RECORDS TO AVOID REPEATED QUERIES !!
  public async Task<IEnumerable<IEnumerable<MarketCapDataDto>>> ListHistoricalMany(string quoteSymbol, int hours = 24)
  {
    _logger.LogDebug("Listing many historical market cap for '{QuoteSymbol}' ..", quoteSymbol);

    var sqlConn = await GetConnection();

    try
    {
      string sqlQuery = @"
SELECT * FROM MarketCapData
WHERE
  QuoteSymbol = @QuoteSymbol
  AND Updated >= @UpdatedSince
  AND BaseSymbol IN (
    SELECT BaseSymbol FROM MarketCapData
    WHERE
      QuoteSymbol = @QuoteSymbol
      AND Updated >= @UpdatedRecent
    GROUP BY BaseSymbol
    ORDER BY Updated DESC )
ORDER BY Updated DESC;";

      // Fetch recent records to determine relevant assets.
      var listHistorical = await sqlConn.QueryAsync<MarketCapDataDb>(sqlQuery, new
      {
        QuoteSymbol = quoteSymbol.ToUpper(),
        UpdatedRecent = DateTime.UtcNow.AddHours(-(Math.Min(2, hours) + earlierTolerance / 60)),
        UpdatedSince = DateTime.UtcNow.AddHours(-(hours + earlierTolerance / 60)),
      });

      // Group by asset base symbol.
      var assetGroups = listHistorical.GroupBy(record => record.BaseSymbol);

      // For each unique asset base symbol, return its historical market cap.
      return assetGroups.Select(assetGroup =>
      {
        var market = new MarketReqDto(quoteSymbol, assetGroup.Key);

        return _mapper.Map<IEnumerable<MarketCapDataDto>>(assetGroup.AsEnumerable());
      });
    }
    finally
    {
      await sqlConn.CloseAsync();
    }
  }
}