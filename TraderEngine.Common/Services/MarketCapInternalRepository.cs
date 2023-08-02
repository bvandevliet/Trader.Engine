using AutoMapper;
using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using TraderEngine.Common.Abstracts;
using TraderEngine.Common.DTOs.Database;
using TraderEngine.Common.DTOs.Request;
using TraderEngine.Common.DTOs.Response;

namespace TraderEngine.Common.Services;

public class MarketCapInternalRepository : MarketCapHandlingBase, IMarketCapInternalRepository
{
  private readonly ILogger<MarketCapInternalRepository> _logger;
  private readonly IMapper _mapper;
  private readonly MySqlConnection _mySqlConnection;

  public MarketCapInternalRepository(
    ILogger<MarketCapInternalRepository> logger,
    IMapper mapper,
    MySqlConnection mySqlConnection)
  {
    _logger = logger;
    _mapper = mapper;
    _mySqlConnection = mySqlConnection;
  }

  /// <summary>
  /// Test whether the record meets the updated time requirement in order to be inserted to the database.
  /// </summary>
  /// <param name="marketCap"></param>
  /// <returns></returns>
  protected async Task<bool> ShouldInsert(MarketCapDataDto marketCap)
  {
    var lastRecord = await _mySqlConnection.QueryFirstOrDefaultAsync<MarketCapDataDb>(
      "SELECT * FROM TraderEngine.MarketCap\n" +
      "WHERE QuoteSymbol = @QuoteSymbol AND BaseSymbol = @BaseSymbol\n" +
      "ORDER BY Updated DESC LIMIT 1;",
      new { marketCap.Market.QuoteSymbol, marketCap.Market.BaseSymbol });

    return IsCloseToTheWholeHour(marketCap.Updated) &&
      (null == lastRecord || OffsetMinutes(marketCap.Updated, lastRecord.Updated) + laterTolerance >= 60 - earlierTolerance);
  }

  public async Task Insert(MarketCapDataDto marketCap)
  {
    var shouldInsert = await ShouldInsert(marketCap);

    if (shouldInsert)
    {
      int rowsAffected = await _mySqlConnection.InsertAsync(_mapper.Map<MarketCapDataDb>(marketCap));

      if (0 == rowsAffected)
      {
        _logger.LogError("Failed to insert {marketCap} to database.", marketCap);
      }
    }
  }

  public async Task InsertMany(IEnumerable<MarketCapDataDto> marketCaps)
  {
    foreach (MarketCapDataDto marketCap in marketCaps)
    {
      await Insert(marketCap);
    }

    _logger.LogInformation("Updated market cap records in database.");
  }

  public async Task<IEnumerable<MarketCapDataDto>> ListHistorical(MarketReqDto market, int days = 21)
  {
    var listHistorical = await _mySqlConnection.QueryAsync<MarketCapDataDb>(
      "SELECT * FROM TraderEngine.MarketCap\n" +
      "WHERE QuoteSymbol = @QuoteSymbol AND BaseSymbol = @BaseSymbol\n" +
      "AND Updated >= @Updated ORDER BY Updated DESC;",
      new { market.QuoteSymbol, market.BaseSymbol, Updated = DateTime.UtcNow.AddDays(-(days + earlierTolerance / 1440)) });

    return _mapper.Map<IEnumerable<MarketCapDataDto>>(listHistorical);
  }

  public async IAsyncEnumerable<IEnumerable<MarketCapDataDto>> ListHistoricalMany(string quoteSymbol, int days = 21)
  {
    var listHistorical = await _mySqlConnection.QueryAsync<MarketCapDataDb>(
      "SELECT * FROM TraderEngine.MarketCap\n" +
      "WHERE QuoteSymbol = @QuoteSymbol\n" +
      "AND Updated >= @Updated ORDER BY Updated DESC;",
      new { quoteSymbol, Updated = DateTime.UtcNow.AddDays(-(1 + earlierTolerance / 1440)) });

    // Group by asset base symbol.
    IEnumerable<IGrouping<string, MarketCapDataDb>> assetGroups =
      listHistorical.GroupBy(record => record.BaseSymbol);

    // For each unique asset base symbol, return its historical market cap.
    foreach (IGrouping<string, MarketCapDataDto> assetGroup in assetGroups)
    {
      var market = new MarketReqDto(quoteSymbol, assetGroup.Key);

      yield return await ListHistorical(market, days);
    }
  }
}