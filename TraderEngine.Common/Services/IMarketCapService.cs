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
  /// </summary>
  /// <param name="quoteSymbol"></param>
  /// <param name="smoothing"></param>
  /// <returns></returns>
  Task<IEnumerable<MarketCapDataDto>> ListLatest(string quoteSymbol, int smoothing);

  /// <summary>
  /// Get the top ranked balanced allocations for the specified <paramref name="configReqDto"/>.
  /// </summary>
  /// <param name="quoteSymbol"></param>
  /// <param name="configReqDto"></param>
  /// <returns></returns>
  Task<IEnumerable<AbsAllocReqDto>> BalancedAllocations(string quoteSymbol, ConfigReqDto configReqDto);
}