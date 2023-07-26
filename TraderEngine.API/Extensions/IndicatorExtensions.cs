using Skender.Stock.Indicators;
using TraderEngine.API.Extensions;
using TraderEngine.Common.DTOs.Response;

namespace TraderEngine.API.Extensions;

public static partial class IndicatorExtensions
{
  public static IEnumerable<EmaResult> GetEma(this IEnumerable<MarketCapData> marketCaps, int lookbackPeriods)
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
  public static double TryGetEmaValue(this IEnumerable<MarketCapData> marketCaps, int lookbackPeriods)
  {
    lookbackPeriods = Math.Max(1, Math.Min(marketCaps.Count(), lookbackPeriods));

    return !marketCaps.Any()
      ? 0
      : marketCaps.GetEma(Math.Min(marketCaps.Count(), lookbackPeriods)).Last().Ema ?? 0;
  }
}