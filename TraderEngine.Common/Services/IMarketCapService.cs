using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;

namespace TraderEngine.Common.Services;

/// <summary>
/// Aggregates internal market cap data.
/// </summary>
public interface IMarketCapService
{
  /// <summary>
  /// Get historical market cap data for the specified market for each day within given amount of days ago.
  /// </summary>
  /// <param name="market"></param>
  /// <param name="days"></param>
  /// <returns></returns>
  Task<IEnumerable<MarketCapDataDto>> ListHistorical(MarketReqDto market, int days = 21);

  /// <summary>
  /// Get historical market cap data of top ranked base currencies for the specified quote currency for each day within given amount of days ago.
  /// </summary>
  /// <param name="quoteSymbol"></param>
  /// <param name="days"></param>
  /// <returns></returns>
  IAsyncEnumerable<IEnumerable<MarketCapDataDto>> ListHistoricalMany(string quoteSymbol, int days = 21);

  /// <summary>
  /// Get the latest market cap data of top ranked base currencies for the specified quote currency,
  /// smoothing out volatility using an Exponential Moving Average of given amount of smoothing days.
  /// This method can use caching for re-use within its lifetime.
  /// </summary>
  /// <param name="quoteSymbol"></param>
  /// <param name="smoothing"></param>
  /// <param name="caching"></param>
  /// <returns></returns>
  Task<IEnumerable<MarketCapDataDto>> ListLatest(string quoteSymbol, int smoothing, bool caching = false);

  /// <summary>
  /// Get the top ranked balanced allocations for the specified configuration.
  /// This method can use caching for re-use within its lifetime.
  /// </summary>
  /// <param name="configReqDto"></param>
  /// <param name="caching"></param>
  /// <returns></returns>
  Task<IEnumerable<AbsAllocReqDto>> BalancedAllocations(ConfigReqDto configReqDto, bool caching = false);
}