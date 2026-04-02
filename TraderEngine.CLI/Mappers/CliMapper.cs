using Riok.Mapperly.Abstractions;
using TraderEngine.CLI.DTOs.CMC;
using TraderEngine.CLI.DTOs.WordPress;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;

namespace TraderEngine.CLI.Mappers;

[Mapper]
public partial class CliMapper : ICliMapper
{
  // ── CMCAssetDto → MarketCapDataDto ───────────────────────────────────────────

  public MarketCapDataDto MapCMCAsset(CMCAssetDto source)
  {
    var firstQuote = source.Quote.FirstOrDefault();
    return new MarketCapDataDto
    {
      Market = new MarketReqDto(firstQuote.Key, source.Symbol),
      Price = (double)firstQuote.Value.Price,
      MarketCap = (double)firstQuote.Value.Market_Cap,
      Tags = source.Tags.ToList(),
      Updated = source.Last_Updated,
    };
  }

  public IEnumerable<MarketCapDataDto> MapCMCAssets(IEnumerable<CMCAssetDto> source)
  {
    return source.Select(MapCMCAsset);
  }

  // ── WordPressConfigDto → ConfigReqDto ────────────────────────────────────────

  [MapProperty(nameof(WordPressConfigDto.quote_takeout), nameof(ConfigReqDto.QuoteTakeout))]
  [MapProperty(nameof(WordPressConfigDto.quote_allocation), nameof(ConfigReqDto.QuoteAllocation))]
  [MapProperty(nameof(WordPressConfigDto.alt_weighting_factors), nameof(ConfigReqDto.AltWeightingFactors))]
  [MapProperty(nameof(WordPressConfigDto.tags_to_include), nameof(ConfigReqDto.TagsToInclude))]
  [MapProperty(nameof(WordPressConfigDto.tags_to_ignore), nameof(ConfigReqDto.TagsToIgnore))]
  [MapProperty(nameof(WordPressConfigDto.top_ranking_count), nameof(ConfigReqDto.TopRankingCount))]
  [MapProperty(nameof(WordPressConfigDto.smoothing), nameof(ConfigReqDto.Smoothing))]
  [MapProperty(nameof(WordPressConfigDto.nth_root), nameof(ConfigReqDto.NthRoot))]
  [MapProperty(nameof(WordPressConfigDto.minimum_diff_quote), nameof(ConfigReqDto.MinimumDiffQuote))]
  [MapProperty(nameof(WordPressConfigDto.minimum_diff_allocation), nameof(ConfigReqDto.MinimumDiffAllocation))]
  [MapProperty(nameof(WordPressConfigDto.automation_enabled), nameof(ConfigReqDto.AutomationEnabled))]
  [MapProperty(nameof(WordPressConfigDto.interval_hours), nameof(ConfigReqDto.IntervalHours))]
  [MapProperty(nameof(WordPressConfigDto.current_alloc_weighting_mult), nameof(ConfigReqDto.CurrentAllocWeightingMult))]
  [MapProperty(nameof(WordPressConfigDto.last_rebalance), nameof(ConfigReqDto.LastRebalance))]
  public partial ConfigReqDto MapConfig(WordPressConfigDto source);

  // ── ConfigReqDto → WordPressConfigDto ────────────────────────────────────────

  [MapProperty(nameof(ConfigReqDto.QuoteTakeout), nameof(WordPressConfigDto.quote_takeout))]
  [MapProperty(nameof(ConfigReqDto.QuoteAllocation), nameof(WordPressConfigDto.quote_allocation))]
  [MapProperty(nameof(ConfigReqDto.AltWeightingFactors), nameof(WordPressConfigDto.alt_weighting_factors))]
  [MapProperty(nameof(ConfigReqDto.TagsToInclude), nameof(WordPressConfigDto.tags_to_include))]
  [MapProperty(nameof(ConfigReqDto.TagsToIgnore), nameof(WordPressConfigDto.tags_to_ignore))]
  [MapProperty(nameof(ConfigReqDto.TopRankingCount), nameof(WordPressConfigDto.top_ranking_count))]
  [MapProperty(nameof(ConfigReqDto.Smoothing), nameof(WordPressConfigDto.smoothing))]
  [MapProperty(nameof(ConfigReqDto.NthRoot), nameof(WordPressConfigDto.nth_root))]
  [MapProperty(nameof(ConfigReqDto.MinimumDiffQuote), nameof(WordPressConfigDto.minimum_diff_quote))]
  [MapProperty(nameof(ConfigReqDto.MinimumDiffAllocation), nameof(WordPressConfigDto.minimum_diff_allocation))]
  [MapProperty(nameof(ConfigReqDto.AutomationEnabled), nameof(WordPressConfigDto.automation_enabled))]
  [MapProperty(nameof(ConfigReqDto.IntervalHours), nameof(WordPressConfigDto.interval_hours))]
  [MapProperty(nameof(ConfigReqDto.CurrentAllocWeightingMult), nameof(WordPressConfigDto.current_alloc_weighting_mult))]
  [MapProperty(nameof(ConfigReqDto.LastRebalance), nameof(WordPressConfigDto.last_rebalance))]
  public partial WordPressConfigDto MapConfigReverse(ConfigReqDto source);
}
