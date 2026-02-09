using System.Text.RegularExpressions;
using AnyClone;
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

  public async Task<IEnumerable<AbsAllocReqDto>?> BalancedAbsAllocs(string quoteSymbol, ConfigReqDto configReqDto, List<MarketReqDto>? currentAssets = null)
  {
    currentAssets = currentAssets?.Clone();

    var marketCapLatest = (await ListLatest(quoteSymbol, configReqDto.Smoothing)).ToList();

    // Expected to have at least 100 records, and one of them BTC. Bail out for safety.
    if (marketCapLatest.Count < 100 || !marketCapLatest.Any(latest => latest.Market.BaseSymbol == "BTC"))
    {
      _logger.LogWarning("No recent market cap records found.");

      return null;
    }

    var includeTagsPattern = configReqDto.TagsToInclude.Any() ?
      string.Join('|', configReqDto.TagsToInclude.Select(tag => $@"^(.*[-_\s])?({tag})([-_\s].*)?$")) : ".*";
    var includeTagsRegex = new Regex(includeTagsPattern, RegexOptions.IgnoreCase);

    var ignoreTagsPattern = string.Join('|', configReqDto.TagsToIgnore.Select(tag => $@"^(.*[-_\s])?({tag})([-_\s].*)?$"));
    var ignoreTagsRegex = new Regex(ignoreTagsPattern, RegexOptions.IgnoreCase);

    return
      marketCapLatest

      // Determine weighting.
      .Select(marketCapDataDto =>
      {
        var hasWeighting = configReqDto.AltWeightingFactors.TryGetValue(marketCapDataDto.Market.BaseSymbol, out var weighting);
        var isAllocated = null != currentAssets?.FindAndRemove(curAlloc => curAlloc.Equals(marketCapDataDto.Market));
        var finalWeighting = hasWeighting ? weighting : 1;

        return new
        {
          MarketCapDataDto = marketCapDataDto,
          HasWeighting = hasWeighting,
          Weighting = finalWeighting,
          OrderByWeighting = finalWeighting * (isAllocated ? configReqDto.CurrentAllocWeightingMult : 1),
        };
      })

      // Skip zero-weighted assets.
      .Where(marketCap => marketCap.Weighting > 0)

      // Handle included tags, but if asset has a weighting configured explicitly, that takes precedence.
      .Where(marketCap => marketCap.HasWeighting || marketCap.MarketCapDataDto.Tags.Any(tag => includeTagsRegex.IsMatch(tag)))

      // Handle ignored tags, but if asset has a weighting configured explicitly, that takes precedence.
      .Where(marketCap => marketCap.HasWeighting || !marketCap.MarketCapDataDto.Tags.Any(tag => ignoreTagsRegex.IsMatch(tag)))

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
          OrderByAbsAlloc = (decimal)Math.Pow(Math.Max(0, marketCap.OrderByWeighting) * marketCap.MarketCapDataDto.MarketCap, 1 / configReqDto.NthRoot),
        };
      })

      // Sort by weighted Market Cap EMA value.
      .OrderByDescending(alloc => alloc.OrderByAbsAlloc)

      // Return absolute allocations.
      .Select(alloc => alloc.AbsAllocDto);
  }
}