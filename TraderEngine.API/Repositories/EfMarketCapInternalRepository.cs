using Microsoft.EntityFrameworkCore;
using TraderEngine.API.Mappers;
using TraderEngine.Common.Abstracts;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Repositories;
using TraderEngine.Data;

namespace TraderEngine.API.Repositories;

/// <summary>
/// Uses <see cref="IDbContextFactory{TContext}"/> rather than a directly injected, DI-scoped
/// <see cref="TraderEngineDbContext"/>: <see cref="TryInsertMany"/> runs many <see cref="TryInsert"/>
/// calls concurrently via <c>Task.WhenAll</c>, and a single <see cref="TraderEngineDbContext"/>
/// instance is not thread-safe for concurrent operations. Each call gets its own, short-lived
/// context instance instead.
/// </summary>
public class EfMarketCapInternalRepository : MarketCapHandlingBase, IMarketCapInternalRepository
{
  private readonly ILogger<EfMarketCapInternalRepository> _logger;
  private readonly IDbContextFactory<TraderEngineDbContext> _dbContextFactory;

  public EfMarketCapInternalRepository(
    ILogger<EfMarketCapInternalRepository> logger,
    IDbContextFactory<TraderEngineDbContext> dbContextFactory)
  {
    _logger = logger;
    _dbContextFactory = dbContextFactory;
  }

  public async Task<int> CleanupDatabase(int daysRetention = 14)
  {
    _logger.LogDebug("Cleaning up market cap database ..");

    await using var db = await _dbContextFactory.CreateDbContextAsync();

    var retentionDate = DateTime.UtcNow.AddDays(-daysRetention);

    var rowsAffected = await db.MarketCapMetrics
      .Where(m => m.Updated < retentionDate)
      .ExecuteDeleteAsync();

    _logger.LogInformation("Cleaned up {rows} stale records from market cap database table.", rowsAffected);

    return rowsAffected;
  }

  public async Task<int> TryInsert(MarketCapDataDto marketCap)
  {
    _logger.LogTrace("Inserting market cap record of '{market}' to database ..", marketCap.Market);

    if (!IsCloseToTheWholeHour(marketCap.Updated))
    {
      _logger.LogWarning("Updated time '{updated}' of market cap of '{market}' is not close to the whole hour.",
        marketCap.Updated, marketCap.Market);
    }

    await using var db = await _dbContextFactory.CreateDbContextAsync();

    var lastRecord = await db.MarketCapMetrics
      .Where(m => m.QuoteSymbol == marketCap.Market.QuoteSymbol && m.BaseSymbol == marketCap.Market.BaseSymbol)
      .OrderByDescending(m => m.Updated)
      .FirstOrDefaultAsync();

    if (null != lastRecord && OffsetMinutes(marketCap.Updated, lastRecord.Updated) + laterTolerance < 60 - earlierTolerance)
    {
      _logger.LogWarning("Updated time '{updated}' of market cap of '{market}' is too close to the previous record.",
        marketCap.Updated, marketCap.Market);

      var rowsDeleted = await db.MarketCapMetrics
        .Where(m => m.QuoteSymbol == marketCap.Market.QuoteSymbol && m.BaseSymbol == marketCap.Market.BaseSymbol)
        .ExecuteDeleteAsync();

      _logger.LogTrace("Deleted '{rows}' old records of market cap of '{market}' from database.",
        rowsDeleted, marketCap.Market);
    }

    _logger.LogTrace("Inserting new market cap record of '{market}' to database ..", marketCap.Market);

    db.MarketCapMetrics.Add(EfMarketCapMapper.MapToEntity(marketCap));

    var rowsAffected = await db.SaveChangesAsync();

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

  public async Task<int> TryInsertMany(IEnumerable<MarketCapDataDto> marketCaps)
  {
    _logger.LogDebug("Inserting market cap records into database ..");

    var rowsAffected = 0;

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

    await using var db = await _dbContextFactory.CreateDbContextAsync();

    var updatedSince = DateTime.UtcNow.AddHours(-(hours + earlierTolerance / 60));

    var listHistorical = await db.MarketCapMetrics
      .AsNoTracking()
      .Where(m => m.QuoteSymbol == market.QuoteSymbol && m.BaseSymbol == market.BaseSymbol && m.Updated >= updatedSince)
      .OrderByDescending(m => m.Updated)
      .ToListAsync();

    return EfMarketCapMapper.MapFromEntities(listHistorical);
  }

  // TODO: CACHE RECENT RECORDS TO AVOID REPEATED QUERIES !!
  public async Task<IEnumerable<IEnumerable<MarketCapDataDto>>> ListHistoricalMany(string quoteSymbol, int hours = 24)
  {
    _logger.LogDebug("Listing many historical market cap for '{QuoteSymbol}' ..", quoteSymbol);

    await using var db = await _dbContextFactory.CreateDbContextAsync();

    var quoteSymbolUpper = quoteSymbol.ToUpper();
    var updatedRecent = DateTime.UtcNow.AddHours(-(Math.Min(2, hours) + earlierTolerance / 60));
    var updatedSince = DateTime.UtcNow.AddHours(-(hours + earlierTolerance / 60));

    // Determine relevant assets from recent records first.
    var relevantAssets = await db.MarketCapMetrics
      .AsNoTracking()
      .Where(m => m.QuoteSymbol == quoteSymbolUpper && m.Updated >= updatedRecent)
      .Select(m => m.BaseSymbol)
      .Distinct()
      .ToListAsync();

    var listHistorical = await db.MarketCapMetrics
      .AsNoTracking()
      .Where(m => m.QuoteSymbol == quoteSymbolUpper && m.Updated >= updatedSince && relevantAssets.Contains(m.BaseSymbol))
      .OrderByDescending(m => m.Updated)
      .ToListAsync();

    // Group by asset base symbol.
    var assetGroups = listHistorical.GroupBy(record => record.BaseSymbol);

    // For each unique asset base symbol, return its historical market cap.
    return assetGroups.Select(assetGroup => EfMarketCapMapper.MapFromEntities(assetGroup.AsEnumerable()));
  }
}
