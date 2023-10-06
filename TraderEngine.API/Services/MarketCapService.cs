using TraderEngine.API.Extensions;
using TraderEngine.Common.Abstracts;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Repositories;

namespace TraderEngine.API.Services;

public class MarketCapService : MarketCapHandlingBase, IMarketCapService
{
  private readonly ILogger<MarketCapService> _logger;
  private readonly IMarketCapInternalRepository _marketCapInternalRepo;

  private readonly object _cacheLock = new();
  private readonly Dictionary<string, List<MarketCapDataDto>> _listLatestSmoothedCache = new();

  public MarketCapService(
    ILogger<MarketCapService> logger,
    IMarketCapInternalRepository marketCapInternalRepo)
  {
    _logger = logger;
    _marketCapInternalRepo = marketCapInternalRepo ?? throw new ArgumentNullException(nameof(marketCapInternalRepo));
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

  public async Task<List<MarketCapDataDto>> ListLatest(string quoteSymbol, int smoothing = 7)
  {
    // Concat the cache indexer string.
    string cacheHash = $"{quoteSymbol}-{smoothing}";

    // Check if exists in cache to avoid expensive re-calculations.
    if (!_listLatestSmoothedCache.ContainsKey(cacheHash))
    {
      // Execute the expensive calculations in an async manner.
      await Task.Run(() =>
      {
        // Avoid race condition.
        lock (_cacheLock)
        {
          // Generate a list containing only the last EMA value for each asset.
          List<MarketCapDataDto> listLatestSmoothed =
            ListHistoricalMany(quoteSymbol, smoothing + 1)
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
            .OrderByDescending(marketCap => marketCap.MarketCap)

            // Enumerate once, add the static list to cache.
            .ToEnumerable().ToList();

          // Add to cache.
          _listLatestSmoothedCache.Add(cacheHash, listLatestSmoothed);
        }
      });
    }

    // Return from cache.
    return _listLatestSmoothedCache[cacheHash];
  }
}