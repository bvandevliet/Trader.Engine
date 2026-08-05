using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Migration.WordPress;

namespace TraderEngine.Migration.Mappers;

public static class WordPressConfigMapper
{
  public static ConfigReqDto Map(WordPressConfigDto source)
  {
    return new()
    {
      QuoteTakeout = source.quote_takeout,
      QuoteAllocation = source.quote_allocation,
      AltWeightingFactors = source.alt_weighting_factors,
      TagsToInclude = source.tags_to_include,
      TagsToIgnore = source.tags_to_ignore,
      TopRankingCount = source.top_ranking_count,
      Smoothing = source.smoothing,
      NthRoot = source.nth_root,
      MinimumDiffQuote = source.minimum_diff_quote,
      MinimumDiffAllocation = source.minimum_diff_allocation,
      AutomationEnabled = source.automation_enabled,
      IntervalHours = source.interval_hours,
      CurrentAllocWeightingMult = source.current_alloc_weighting_mult,
      // WordPressDbSerializer's DateTime deserialization doesn't restore Kind (always comes back
      // Unspecified, see its "Handle timezone !!" TODO) — Npgsql rejects Unspecified for
      // "timestamp with time zone". The legacy scheduler that wrote this value always used UTC,
      // so that's the only sound interpretation here.
      LastRebalance = source.last_rebalance.HasValue
        ? DateTime.SpecifyKind(source.last_rebalance.Value, DateTimeKind.Utc)
        : null,
    };
  }
}
