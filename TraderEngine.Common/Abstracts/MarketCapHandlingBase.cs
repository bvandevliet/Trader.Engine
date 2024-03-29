namespace TraderEngine.Common.Abstracts;

public abstract class MarketCapHandlingBase
{
  /// <summary>
  /// Tolerance in minutes a record is allowed to be too early in time to be considered a candidate.
  /// </summary>
  protected static readonly int earlierTolerance = 6;

  /// <summary>
  /// Tolerance in minutes a record is allowed to be too late in time to be considered a candidate.
  /// </summary>
  protected static readonly int laterTolerance = 9;

  /// <summary>
  /// Get the amount of hours a given date lies in the past from UTC now.
  /// </summary>
  /// <param name="leadTime"></param>
  /// <param name="baseTime"></param>
  /// <returns></returns>
  protected static double OffsetMinutes(DateTime leadTime, DateTime? baseTime = null)
  {
    baseTime ??= DateTime.UtcNow;

    return (leadTime - baseTime).Value.TotalMinutes;
  }

  /// <summary>
  /// Test whether the given date is close enough to the whole hour.
  /// </summary>
  /// <param name="dateTime"></param>
  /// <returns></returns>
  protected static bool IsCloseToTheWholeHour(DateTime dateTime)
  {
    return dateTime.Minute >= 60 - earlierTolerance || dateTime.Minute <= laterTolerance;
  }
}
