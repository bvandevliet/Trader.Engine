using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using TraderEngine.Common.Abstracts;
using TraderEngine.Common.AppSettings;
using TraderEngine.Common.DTOs.Request;
using TraderEngine.Common.DTOs.Response;

namespace TraderEngine.Common.Services;

public class MarketCapInternalRepository : MarketCapHandlingBase, IMarketCapInternalRepository
{
  private readonly ILogger<MarketCapInternalRepository> _logger;

  private readonly IMongoCollection<MarketCapData> _marketCapRecords;

  public MarketCapInternalRepository(
    ILogger<MarketCapInternalRepository> logger,
    IOptions<MongoSettings> mongoSettings,
    IMongoClient mongoClient)
  {
    _logger = logger;

    IMongoDatabase marketCapDb = mongoClient.GetDatabase(mongoSettings.Value.Databases.MetricData.MarketCap);
    _marketCapRecords = marketCapDb.GetCollection<MarketCapData>($"{mongoSettings.Value.Databases.MetricData.MarketCap}Collection");
  }

  protected static readonly FindOptions<MarketCapData> findDescByDate = new()
  {
    Sort = Builders<MarketCapData>.Sort.Descending(record => record.Updated),
  };

  protected static readonly FindOptions<MarketCapData> limitForLatest = new()
  {
    Sort = Builders<MarketCapData>.Sort.Descending(record => record.Updated),
    Limit = 1,
  };

  protected static FilterDefinition<MarketCapData> FilterWithinDays(int days) =>
    Builders<MarketCapData>.Filter
    .Gte(record => record.Updated, DateTime.UtcNow.AddDays(-(days + earlierTolerance / 1440)));

  protected static FilterDefinition<MarketCapData> FilterEqualMarket(MarketDto market) =>
    Builders<MarketCapData>.Filter
    .Eq(record => record.Market.QuoteSymbol, market.QuoteSymbol) &
    Builders<MarketCapData>.Filter
    .Eq(record => record.Market.BaseSymbol, market.BaseSymbol);

  /// <summary>
  /// Test whether the record meets the updated time requirement in order to be inserted to the database.
  /// </summary>
  /// <param name="marketCap"></param>
  /// <returns></returns>
  protected async Task<bool> ShouldInsert(MarketCapData marketCap)
  {
    IAsyncCursor<MarketCapData> records =
      await _marketCapRecords.FindAsync(FilterEqualMarket(marketCap.Market), limitForLatest);

    MarketCapData? lastRecord = await records.FirstOrDefaultAsync();

    return IsCloseToTheWholeHour(marketCap.Updated) &&
      (null == lastRecord || OffsetMinutes(marketCap.Updated, lastRecord.Updated) + laterTolerance >= 60 - earlierTolerance);
  }

  public async Task Insert(MarketCapData marketCap)
  {
    if (await ShouldInsert(marketCap))
    {
      Task result = _marketCapRecords.InsertOneAsync(marketCap);

      try
      {
        await result;

        if (!result.IsCompletedSuccessfully)
        {
          _logger.LogError("Failed to insert {marketCap} to database.", marketCap);
        }

        var exs = result.Exception?.InnerExceptions;
        for (int i = 0; i < exs?.Count; i++)
        {
          Exception ex = exs[i];
          _logger.LogError(ex, ex.Message);
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, ex.Message);
      }
    }
  }

  public async Task InsertMany(IEnumerable<MarketCapData> marketCaps)
  {
    foreach (MarketCapData marketCap in marketCaps)
    {
      await Insert(marketCap);
    }

    _logger.LogInformation("Updated market cap records in database.");
  }

  public async Task<IEnumerable<MarketCapData>> ListHistorical(MarketDto market, int days = 21)
  {
    var filterBuilder = Builders<MarketCapData>.Filter;
    var filter = filterBuilder.Empty;

    // Filter by market.
    filter &= FilterEqualMarket(market);

    // Only assets that were updated within the given amount of days are considered relevant.
    filter &= FilterWithinDays(days);

    // Get historical market cap data.
    IAsyncCursor<MarketCapData> records =
      await _marketCapRecords.FindAsync(filter, findDescByDate);

    // Filter by market.
    return records.ToEnumerable();
  }

  public async IAsyncEnumerable<IEnumerable<MarketCapData>> ListHistoricalMany(string quoteSymbol, int days = 21)
  {
    var filterBuilder = Builders<MarketCapData>.Filter;
    var filter = filterBuilder.Empty;

    // Filter by quote symbol.
    filter &= filterBuilder.Eq(record => record.Market.QuoteSymbol, quoteSymbol);

    // Only assets that were updated in the past 24 hours are considered relevant.
    filter &= FilterWithinDays(1);

    // Get historical market cap data.
    IAsyncCursor<MarketCapData> records =
      await _marketCapRecords.FindAsync(filter, findDescByDate);

    // Group by asset base symbol.
    IEnumerable<IGrouping<string, MarketCapData>> assetGroups =
      records.ToEnumerable().GroupBy(record => record.Market.BaseSymbol);

    // For each unique asset base symbol, return its historical market cap.
    foreach (IGrouping<string, MarketCapData> assetGroup in assetGroups)
    {
      var market = new MarketDto(quoteSymbol, assetGroup.Key);

      yield return await ListHistorical(market, days);
    }
  }
}