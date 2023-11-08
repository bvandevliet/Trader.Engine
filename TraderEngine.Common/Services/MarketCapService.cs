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

  public Task<IEnumerable<MarketCapDataDto>> ListLatest(string quoteSymbol, int smoothing, bool caching = false)
  {
    // Generates a list containing only the last EMA value for each asset.
    IEnumerable<MarketCapDataDto> smooth(IEnumerable<IEnumerable<MarketCapDataDto>> historicalMany)
    {
      return
        historicalMany
        .Select(marketCaps =>
        {
          // Enumerate once, then just iterate.
          var marketCapsList = marketCaps.ToList();

          // Get last market cap record.
          var marketCap = marketCapsList.Last();

          // Update market cap value with EMA value.
          marketCap.MarketCap = marketCapsList.TryGetEmaValue(smoothing);

          // Return altered record.
          return marketCap;
        });
    }

    return Task.Run(() =>
    {
      if (!caching)
      {
        var listHistoricalMany = _marketCapInternalRepo.ListHistoricalMany(quoteSymbol, smoothing + 1).ToEnumerable();

        return smooth(listHistoricalMany);
      }

      // Concat the smoothing cache indexer string.
      string smoothingCacheHash = $"{quoteSymbol}-{smoothing}";

      // Avoid race condition.
      lock (_listLatestCacheLock)
      {
        // Check if exists in cache to avoid expensive re-calculations.
        if (!_listLatestSmoothedCache.ContainsKey(smoothingCacheHash))
        {
          // Minimum of 48 + 1 records to leverage caching.
          int hours = Math.Max(48, smoothing) + 1;

          // Concat the historical cache indexer string.
          string historicalCacheHash = $"{quoteSymbol}-{hours}";

          // Check if exists in cache to avoid unneeded database querying.
          if (!_listHistoricalManyCache.ContainsKey(historicalCacheHash))
          {
            var listHistoricalMany = _marketCapInternalRepo.ListHistoricalMany(quoteSymbol, hours).ToEnumerable();

            _listHistoricalManyCache.Add(historicalCacheHash, listHistoricalMany.ToList());
          }

          var listLatestSmoothed = smooth(_listHistoricalManyCache[historicalCacheHash]);

          // Add to cache.
          _listLatestSmoothedCache.Add(smoothingCacheHash, listLatestSmoothed.ToList());
        }
      }

      // Return from cache.
      return _listLatestSmoothedCache[smoothingCacheHash];
    });
  }

  public async Task<IEnumerable<AbsAllocReqDto>> BalancedAllocations(ConfigReqDto configReqDto, bool caching = false)
  {
    var marketCapLatest = await ListLatest(configReqDto.QuoteCurrency, configReqDto.Smoothing, caching);

    return
      marketCapLatest

      // Handle ignored tags.
      .Where(marketCap => !marketCap.Tags.Intersect(configReqDto.TagsToIgnore).Any())

      // Apply weighting and dampening.
      .Select(marketCap =>
      {
        decimal weighting =
          configReqDto.AltWeightingFactors.GetValueOrDefault(marketCap.Market.BaseSymbol, 1);

        return new AbsAllocReqDto()
        {
          BaseSymbol = marketCap.Market.BaseSymbol,
          AbsAlloc = weighting * (decimal)Math.Pow(marketCap.MarketCap, 1 / configReqDto.NthRoot),
        };
      })

      // Sort by Market Cap EMA value.
      .OrderByDescending(alloc => alloc.AbsAlloc)

      // Take the top count.
      .Take(configReqDto.TopRankingCount);
  }
}