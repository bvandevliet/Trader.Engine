using Microsoft.Extensions.Logging;
using TraderEngine.Common.Abstracts;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Extensions;
using TraderEngine.Common.Repositories;

namespace TraderEngine.Common.Services;

/// <summary>
/// Register as either scoped or transient for proper internal caching.
/// </summary>
public class MarketCapService : MarketCapHandlingBase, IMarketCapService
{
  private readonly ILogger<MarketCapService> _logger;
  private readonly IMarketCapInternalRepository _marketCapInternalRepo;

  private readonly object _listLatestCacheLock = new();
  private readonly Dictionary<string, List<IEnumerable<MarketCapDataDto>>> _listHistoricalManyCache = new();
  private readonly Dictionary<string, List<MarketCapDataDto>> _listLatestSmoothedCache = new();

  public MarketCapService(
    ILogger<MarketCapService> logger,
    IMarketCapInternalRepository marketCapInternalRepo)
  {
    _logger = logger;
    _marketCapInternalRepo = marketCapInternalRepo;
  }

  /// <summary>
  /// Finds a record for each day in the provided records
  /// as long as the record is within the allowed tolerance.
  /// </summary>
  /// <param name="records"></param>
  /// <param name="days"></param>
  /// <returns></returns>
  private IEnumerable<MarketCapDataDto> GetCandidates(IEnumerable<MarketCapDataDto> records)
  {
    // Updated timestamp of last winner record.
    DateTime? prevUpdated = null;

    // Potential winner record.
    MarketCapDataDto? candidate = null;

    foreach (MarketCapDataDto record in records)
    {
    start:

      // Offset in minutes between current iteration record and last winner record.
      double offsetMinutes = OffsetMinutes(record.Updated, prevUpdated);

      // Test if current iteration record is within one day from last winner record.
      bool isWithinDayTimespan = offsetMinutes + earlierTolerance >= -1440 - laterTolerance;

      // Test if current iteration record is within and nearly one day from last winner record.
      bool isWithinDailySequence = isWithinDayTimespan && offsetMinutes - laterTolerance <= -1440 + earlierTolerance;

      // If current iteration record is within one day from last winner record,
      // mark it as potential candidate.
      if (isWithinDayTimespan)
      {
        candidate = record;

        // If is NOT first iteration AND current iteration record is NOT exactly one day from last winner record,
        // continue iterating for candidates.
        if (null != prevUpdated && !isWithinDailySequence) { continue; }
      }

      if (null != candidate)
      {
        // This candidate is a winner record.
        yield return candidate;

        // Store updated timestamp.
        prevUpdated = candidate.Updated;

        // Reset candidate to ensure it's only returned once.
        candidate = null;

        // If winner record came from previous iteration, then re-process current iteration record.
        if (!isWithinDayTimespan) { goto start; }
      }

      // Bail as soon as the daily sequence gets interrupted.
      else { break; }
    }
  }

  public async Task<IEnumerable<MarketCapDataDto>> ListHistorical(MarketReqDto market, int days = 21)
  {
    IEnumerable<MarketCapDataDto> records = await _marketCapInternalRepo.ListHistorical(market, days);

    return GetCandidates(records);
  }

  public async IAsyncEnumerable<IEnumerable<MarketCapDataDto>> ListHistoricalMany(string quoteSymbol, int days = 21)
  {
    await foreach (IEnumerable<MarketCapDataDto> marketCaps in _marketCapInternalRepo.ListHistoricalMany(quoteSymbol, days))
    {
      yield return GetCandidates(marketCaps);
    }
  }

  public Task<IEnumerable<MarketCapDataDto>> ListLatest(string quoteSymbol, int smoothing, bool caching = false)
  {
    // Generates a list containing only the last EMA value for each asset.
    Func<IEnumerable<IEnumerable<MarketCapDataDto>>, IEnumerable<MarketCapDataDto>> smooth = historicalMany =>
    {
      return
        historicalMany

        // Only process what's relevant.
        .Take(smoothing + 1)
        .Select(marketCaps =>
        {
          // Enumerate once, then just iterate.
          var marketCapsList = marketCaps.ToList();

          // Get last market cap record.
          MarketCapDataDto marketCap = marketCapsList.Last();

          // Update market cap value with EMA value.
          marketCap.MarketCap = marketCapsList.TryGetEmaValue(smoothing);

          // Return altered record.
          return marketCap;
        })

        // Sort by EMA value.
        .OrderByDescending(marketCap => marketCap.MarketCap);
    };

    return Task.Run(() =>
    {
      // Concat the smoothing cache indexer string.
      string smoothingCacheHash = $"{quoteSymbol}-{smoothing}";

      // Check if exists in cache to avoid expensive re-calculations.
      if (!_listLatestSmoothedCache.ContainsKey(smoothingCacheHash))
      {
        if (!caching)
        {
          return smooth(ListHistoricalMany(quoteSymbol, smoothing + 1).ToEnumerable());
        }

        // Avoid race condition.
        lock (_listLatestCacheLock)
        {
          // Minimum of 50 records to leverage caching.
          int days = Math.Max(50, smoothing + 1);

          // Concat the historical cache indexer string.
          string historicalCacheHash = $"{quoteSymbol}-{days}";

          // Check if exists in cache to avoid unneeded database querying.
          if (caching && !_listHistoricalManyCache.ContainsKey(historicalCacheHash))
          {
            _listHistoricalManyCache.Add(
              historicalCacheHash, ListHistoricalMany(quoteSymbol, days).ToEnumerable().ToList());
          }

          IEnumerable<MarketCapDataDto> listLatestSmoothed =
            smooth(_listHistoricalManyCache[historicalCacheHash]);

          // Add to cache.
          _listLatestSmoothedCache.Add(smoothingCacheHash, listLatestSmoothed.ToList());
        }
      }

      // Return from cache.
      return _listLatestSmoothedCache[smoothingCacheHash];
    });
  }
}