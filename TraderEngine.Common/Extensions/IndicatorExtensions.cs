using Skender.Stock.Indicators;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Extensions;

namespace TraderEngine.Common.Extensions;

public static class IndicatorExtensions
{
  public static IEnumerable<EmaResult> GetEma(this IEnumerable<MarketCapDataDto> marketCaps, int lookbackPeriods)
  {
    return marketCaps
      .Select(marketCap => (marketCap.Updated, marketCap.MarketCap))
      .GetEma(lookbackPeriods);
  }

  /// <summary>
  /// Always returns a value, even if the lookback period is greater than the number of records.
  /// </summary>
  /// <param name="marketCaps"></param>
  /// <param name="lookbackPeriods"></param>
  /// <returns></returns>
  public static double TryGetEmaValue(this IEnumerable<MarketCapDataDto> marketCaps, int lookbackPeriods)
  {
    lookbackPeriods = Math.Max(1, Math.Min(marketCaps.Count(), lookbackPeriods));

    return !marketCaps.Any()
      ? 0
      : marketCaps.GetEma(lookbackPeriods).Last().Ema ?? 0;
  }
}