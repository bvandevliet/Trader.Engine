using System.Text.RegularExpressions;
using TraderEngine.Common.Abstracts;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Extensions;
using TraderEngine.Common.Repositories;

namespace TraderEngine.API.Services;

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
    _logger.LogDebug("Listing latest market cap data for '{QuoteSymbol}' ..", quoteSymbol);

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

  public async Task<IEnumerable<AbsAllocReqDto>?> BalancedAbsAllocs(string quoteSymbol, ConfigReqDto configReqDto)
  {
    _logger.LogDebug("Calculating balanced absolute allocations for '{QuoteSymbol}' ..", quoteSymbol);

    var marketCapLatest = (await ListLatest(quoteSymbol, configReqDto.Smoothing)).ToList();

    // Expected to have at least 100 records. Bail out for safety.
    if (marketCapLatest.Count < 100)
    {
      _logger.LogWarning("No recent market cap records found.");

      return null;
    }

    string ignoredTagsPattern = string.Join('|', configReqDto.TagsToIgnore.Select(tag => $"^{tag}$"));

    var ignoredTagsRegex = new Regex(ignoredTagsPattern, RegexOptions.IgnoreCase);

    return
      marketCapLatest

      // Determine weighting.
      .Select(marketCapDataDto =>
      {
        bool hasWeighting = configReqDto.AltWeightingFactors.TryGetValue(marketCapDataDto.Market.BaseSymbol, out double weighting);

        return new
        {
          MarketCapDataDto = marketCapDataDto,
          HasWeighting = hasWeighting,
          Weighting = hasWeighting ? weighting : 1,
        };
      })

      // Skip zero-weighted assets.
      .Where(marketCap => marketCap.Weighting > 0)

      // Handle ignored tags, but not if asset has a weighting configured explicitly.
      .Where(marketCap => marketCap.HasWeighting || !marketCap.MarketCapDataDto.Tags.Any(tag => ignoredTagsRegex.IsMatch(tag)))

      // Apply weighting and dampening.
      .Select(marketCap =>
      {
        return new
        {
          MarketCap = marketCap,
          AbsAllocDto = new AbsAllocReqDto()
          {
            Market = marketCap.MarketCapDataDto.Market,
            AbsAlloc = (decimal)Math.Pow(Math.Max(0, marketCap.Weighting) * marketCap.MarketCapDataDto.MarketCap, 1 / configReqDto.NthRoot),
          },
        };
      })

      // Sort by weighted Market Cap EMA value.
      .OrderByDescending(alloc => alloc.AbsAllocDto.AbsAlloc)

      // Take the top count, and any assets with a weighting.
      .Where(alloc =>
      {
        configReqDto.TopRankingCount--;
        return configReqDto.TopRankingCount >= 0 || alloc.MarketCap.HasWeighting;
      })

      // Return absolute allocations.
      .Select(alloc => alloc.AbsAllocDto);
  }
}