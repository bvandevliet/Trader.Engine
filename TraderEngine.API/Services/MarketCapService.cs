using System.Text.RegularExpressions;
using TraderEngine.API.Mappers;
using TraderEngine.Common.Abstracts;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Extensions;
using TraderEngine.Data.Repositories;

namespace TraderEngine.API.Services;

public class MarketCapService : MarketCapHandlingBase, IMarketCapService
{
  /// <summary>
  /// Upper bound on a single tag-regex match against a single (short) tag string — user-authored
  /// patterns (<see cref="ConfigReqDto.TagsToInclude"/>/<see cref="ConfigReqDto.TagsToIgnore"/>)
  /// could otherwise hang this shared service process via catastrophic backtracking.
  /// </summary>
  private static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromMilliseconds(250);

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

  public async Task<IEnumerable<TargetAllocReqDto>?> BalancedTargetAllocs(string quoteSymbol, ConfigReqDto configReqDto, List<MarketReqDto>? currentAssets = null)
  {
    var marketCapLatest = (await ListLatest(quoteSymbol, configReqDto.Smoothing)).ToList();

    // Expected to have at least 100 records, and one of them BTC. Bail out for safety.
    if (marketCapLatest.Count < 100 || !marketCapLatest.Any(latest => latest.Market.BaseSymbol == "BTC"))
    {
      _logger.LogWarning("No recent market cap records found.");

      return null;
    }

    // Patterns are user-authored regex by design (see ConfigModel.ValidateTagPatterns), so they
    // can't be Regex.Escape()'d without breaking that feature. A bounded match timeout instead
    // guards against a catastrophic-backtracking pattern hanging this shared service process
    // (ReDoS) — CodeQL's cs/regex-injection flags any Regex built from unescaped user input.
    var includeTagsPattern = configReqDto.TagsToInclude.Any() ?
      string.Join('|', configReqDto.TagsToInclude.Select(tag => $@"^(.*[-_\s])?({tag})([-_\s].*)?$")) : ".*";
    var includeTagsRegex = new Regex(includeTagsPattern, RegexOptions.IgnoreCase, RegexMatchTimeout);

    var ignoreTagsPattern = string.Join('|', configReqDto.TagsToIgnore.Select(tag => $@"^(.*[-_\s])?({tag})([-_\s].*)?$"));
    var ignoreTagsRegex = new Regex(ignoreTagsPattern, RegexOptions.IgnoreCase, RegexMatchTimeout);

    currentAssets = currentAssets?.Select(a => a.DeepClone()).ToList();

    return
      marketCapLatest

      // Determine weighting.
      .Select(marketCapDataDto =>
      {
        var hasWeighting = configReqDto.WeightingOverrides.TryGetValue(marketCapDataDto.Market.BaseSymbol, out var weighting);
        var isAllocated = null != currentAssets?.FindAndRemove(curAlloc => curAlloc.Equals(marketCapDataDto.Market));
        var finalWeighting = hasWeighting ? weighting : 1;

        return new
        {
          MarketCapDataDto = marketCapDataDto,
          HasWeighting = hasWeighting,
          Weighting = finalWeighting,
          OrderByWeighting = finalWeighting * (isAllocated ? configReqDto.HeldAssetBiasMult : 1),
        };
      })

      // Skip zero-weighted assets.
      .Where(marketCap => marketCap.Weighting > 0)

      // Handle included tags, but if asset has a weighting configured explicitly, that takes precedence.
      .Where(marketCap => marketCap.HasWeighting || marketCap.MarketCapDataDto.Tags.Any(tag => SafeIsMatch(includeTagsRegex, tag)))

      // Handle ignored tags, but if asset has a weighting configured explicitly, that takes precedence.
      .Where(marketCap => marketCap.HasWeighting || !marketCap.MarketCapDataDto.Tags.Any(tag => SafeIsMatch(ignoreTagsRegex, tag)))

      // Apply weighting and dampening.
      .Select(marketCap => new
      {
        MarketCap = marketCap,
        TargetAllocDto = new TargetAllocReqDto()
        {
          Market = marketCap.MarketCapDataDto.Market,
          TargetWeight = (decimal)Math.Pow(Math.Max(0, marketCap.Weighting) * marketCap.MarketCapDataDto.MarketCap, 1 / configReqDto.NthRoot),
        },
        OrderByTargetWeight = (decimal)Math.Pow(Math.Max(0, marketCap.OrderByWeighting) * marketCap.MarketCapDataDto.MarketCap, 1 / configReqDto.NthRoot),
      })

      // Sort by weighted Market Cap EMA value.
      .OrderByDescending(alloc => alloc.OrderByTargetWeight)

      // Return absolute allocations.
      .Select(alloc => alloc.TargetAllocDto);
  }

  /// <summary>
  /// Wraps <see cref="Regex.IsMatch(string)"/> so a single user-authored tag pattern timing out
  /// against one tag (see <see cref="RegexMatchTimeout"/>) is treated as a non-match rather than
  /// throwing and aborting the whole allocation calculation for every other asset/tag.
  /// </summary>
  private bool SafeIsMatch(Regex regex, string input)
  {
    try
    {
      return regex.IsMatch(input);
    }
    catch (RegexMatchTimeoutException ex)
    {
      _logger.LogWarning(ex, "Tag regex {Pattern} timed out matching against {Tag}; treating as no match.",
        regex.ToString().SanitizeForLog(), input.SanitizeForLog());

      return false;
    }
  }
}