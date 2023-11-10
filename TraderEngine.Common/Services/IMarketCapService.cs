using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;

namespace TraderEngine.Common.Services;

/// <summary>
/// Aggregates internal market cap data.
/// </summary>
public interface IMarketCapService
{
  /// <summary>
  /// Get the latest market cap data of top ranked base currencies for the specified <paramref name="quoteSymbol"/>,
  /// smoothing out volatility using an Exponential Moving Average of given amount of <paramref name="smoothing"/> hours.
  /// This method can use caching for re-use within its lifetime.
  /// </summary>
  /// <param name="quoteSymbol"></param>
  /// <param name="smoothing"></param>
  /// <param name="caching"></param>
  /// <returns></returns>
  Task<IEnumerable<MarketCapDataDto>> ListLatest(string quoteSymbol, int smoothing, bool caching = false);

  /// <summary>
  /// Get the top ranked balanced allocations for the specified <paramref name="configReqDto"/>.
  /// This method can use caching for re-use within its lifetime.
  /// </summary>
  /// <param name="quoteSymbol"></param>
  /// <param name="configReqDto"></param>
  /// <param name="caching"></param>
  /// <returns></returns>
  Task<IEnumerable<AbsAllocReqDto>> BalancedAllocations(string quoteSymbol, ConfigReqDto configReqDto, bool caching = false);
}