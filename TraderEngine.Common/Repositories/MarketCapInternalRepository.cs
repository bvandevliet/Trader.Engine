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

  private MySqlConnection GetConnection() => _sqlConnectionFactory.GetService("MySql");

  public async Task<int> InitDatabase()
  {
    var sqlConn = GetConnection();

    var result = await sqlConn.ExecuteAsync(
        "CREATE TABLE IF NOT EXISTS MarketCapData (\n" +
        "  id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,\n" +
        "  QuoteSymbol VARCHAR(12) NOT NULL,\n" +
        "  BaseSymbol VARCHAR(12) NOT NULL,\n" +
        "  Price VARCHAR(48) NOT NULL,\n" +
        "  MarketCap VARCHAR(48) NOT NULL,\n" +
        "  Tags TEXT,\n" +
        "  Updated DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP);");

    await sqlConn.CloseAsync();

    return result;
  }

  /// <summary>
  /// Test whether the record meets the updated time requirement in order to be inserted to the database.
  /// </summary>
  /// <param name="sqlConn"></param>
  /// <param name="marketCap"></param>
  /// <returns></returns>
  protected static async Task<bool> ShouldInsert(MySqlConnection sqlConn, MarketCapDataDto marketCap)
  {
    if (!IsCloseToTheWholeHour(marketCap.Updated))
    {
      return false;
    }

    var lastRecord = await sqlConn.QueryFirstOrDefaultAsync<MarketCapDataDb>(
      "SELECT * FROM MarketCapData\n" +
      "WHERE QuoteSymbol = @QuoteSymbol AND BaseSymbol = @BaseSymbol\n" +
      "ORDER BY Updated DESC LIMIT 1;",
      new
      {
        marketCap.Market.QuoteSymbol,
        marketCap.Market.BaseSymbol
      });

    return null == lastRecord || OffsetMinutes(marketCap.Updated, lastRecord.Updated) + laterTolerance >= 60 - earlierTolerance;
  }

  /// <summary>
  /// Saves a market cap object to the database.
  /// </summary>
  /// <param name="sqlConn"></param>
  /// <param name="marketCap"></param>
  /// <returns></returns>
  protected async Task<int> Insert(MySqlConnection sqlConn, MarketCapDataDto marketCap)
  {
    int rowsAffected = 0;

    if (await ShouldInsert(sqlConn, marketCap))
    {
      var marketCapData = _mapper.Map<MarketCapDataDb>(marketCap);

      rowsAffected = await sqlConn.ExecuteAsync(
        "INSERT INTO MarketCapData (QuoteSymbol, BaseSymbol, Price, MarketCap, Tags, Updated)\n" +
        "VALUES (@QuoteSymbol, @BaseSymbol, @Price, @MarketCap, @Tags, @Updated);",
        marketCapData);

      if (0 == rowsAffected)
      {
        _logger.LogError("Failed to insert market cap of {market} to database.", marketCap.Market);
      }
    }

    return rowsAffected;
  }

  public async Task<int> InsertMany(IEnumerable<MarketCapDataDto> marketCaps)
  {
    var sqlConn = GetConnection();

    int rowsAffected = 0;

    foreach (var marketCap in marketCaps)
    {
      rowsAffected += await Insert(sqlConn, marketCap);
    }

    await sqlConn.CloseAsync();

    _logger.LogInformation("Inserted {rows} market cap records into database.", rowsAffected);

    return rowsAffected;
  }

  public async Task<IEnumerable<MarketCapDataDto>> ListHistorical(MarketReqDto market, int hours = 24)
  {
    var sqlConn = GetConnection();

    var listHistorical = await sqlConn.QueryAsync<MarketCapDataDb>(
      "SELECT * FROM MarketCapData\n" +
      "WHERE QuoteSymbol = @QuoteSymbol AND BaseSymbol = @BaseSymbol\n" +
      "AND Updated >= @Updated ORDER BY Updated DESC;",
      new
      {
        market.QuoteSymbol,
        market.BaseSymbol,
        Updated = DateTime.UtcNow.AddHours(-(hours + earlierTolerance / 60)),
      });

    await sqlConn.CloseAsync();

    return _mapper.Map<IEnumerable<MarketCapDataDto>>(listHistorical);
  }

  // TODO: CACHE RECENT RECORDS TO AVOID REPEATED QUERIES !!
  public async Task<IEnumerable<IEnumerable<MarketCapDataDto>>> ListHistoricalMany(string quoteSymbol, int hours = 24)
  {
    var sqlConn = GetConnection();

    // Fetch recent records to determine relevant assets.
    var listHistorical = await sqlConn.QueryAsync<MarketCapDataDb>(
      "SELECT * FROM MarketCapData\n" +
      "WHERE QuoteSymbol = @QuoteSymbol AND BaseSymbol IN (\n" +
      "  SELECT BaseSymbol FROM MarketCapData\n" +
      "  WHERE QuoteSymbol = @QuoteSymbol\n" +
      "  AND Updated >= @UpdatedRecent\n" +
      "  GROUP BY BaseSymbol\n" +
      "  ORDER BY Updated DESC)\n" +
      "AND Updated >= @UpdatedSince ORDER BY Updated DESC;",
      new
      {
        QuoteSymbol = quoteSymbol.ToUpper(),
        UpdatedRecent = DateTime.UtcNow.AddHours(-(Math.Min(2, hours) + earlierTolerance / 60)),
        UpdatedSince = DateTime.UtcNow.AddHours(-(hours + earlierTolerance / 60)),
      });

    await sqlConn.CloseAsync();

    // Group by asset base symbol.
    var assetGroups = listHistorical.GroupBy(record => record.BaseSymbol);

    // For each unique asset base symbol, return its historical market cap.
    return assetGroups.Select(assetGroup =>
    {
      var market = new MarketReqDto(quoteSymbol, assetGroup.Key);

      return _mapper.Map<IEnumerable<MarketCapDataDto>>(assetGroup.AsEnumerable());
    });
  }
}