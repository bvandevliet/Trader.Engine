using Microsoft.Extensions.Logging;
using TraderEngine.Common.Abstracts;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Extensions;
using TraderEngine.Common.Repositories;

namespace TraderEngine.Common.Services;

public class MarketCapService : MarketCapHandlingBase, IMarketCapService
{
  private readonly ILogger<MarketCapService> _logger;
  private readonly IMarketCapInternalRepository _marketCapInternalRepo;

  public MarketCapService(
    ILogger<MarketCapService> logger,
    IMarketCapInternalRepository marketCapInternalRepo)
  {
    _logger = logger;
    _marketCapInternalRepo = marketCapInternalRepo;
  }

  public async Task<IEnumerable<MarketCapDataDto>> ListLatest(string quoteSymbol, int smoothing)
  {
    var listHistoricalMany = await _marketCapInternalRepo.ListHistoricalMany(quoteSymbol, smoothing + 1);

    // Generates a list containing only the last EMA value for each asset.
    return listHistoricalMany
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

  public async Task<IEnumerable<AbsAllocReqDto>> BalancedAbsAllocs(string quoteSymbol, ConfigReqDto configReqDto)
  {
    var marketCapLatest = await ListLatest(quoteSymbol, configReqDto.Smoothing);

    return
      marketCapLatest

      // Determine weighting.
      .Select(marketCapDataDto => new
      {
        MarketCapDataDto = marketCapDataDto,
        Weighting = configReqDto.AltWeightingFactors.GetValueOrDefault(marketCapDataDto.Market.BaseSymbol, 1),
      })

      // Skip zero-weighted assets.
      .Where(marketCap => marketCap.Weighting > 0)

      // Handle ignored tags.
      .Where(marketCap => !marketCap.MarketCapDataDto.Tags.Intersect(configReqDto.TagsToIgnore).Any())

      // Apply weighting and dampening.
      .Select(marketCap =>
      {
        return new AbsAllocReqDto()
        {
          BaseSymbol = marketCap.MarketCapDataDto.Market.BaseSymbol,
          AbsAlloc = (decimal)Math.Pow(Math.Max(0, marketCap.Weighting) * marketCap.MarketCapDataDto.MarketCap, 1 / configReqDto.NthRoot),
        };
      })

      // Sort by Market Cap EMA value.
      .OrderByDescending(alloc => alloc.AbsAlloc)

      // Take the top count.
      .Take(configReqDto.TopRankingCount);
  }
}