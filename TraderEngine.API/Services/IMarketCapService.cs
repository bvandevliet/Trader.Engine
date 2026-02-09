using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;

namespace TraderEngine.API.Services;

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
  public Task<IEnumerable<MarketCapDataDto>> ListLatest(string quoteSymbol, int smoothing);

  /// <summary>
  /// Get the top ranked balanced allocations for the specified <paramref name="configReqDto"/>.
  /// Returns null if not at least 100 records are available, which indicates a lack of recent market cap data.
  /// </summary>
  /// <param name="quoteSymbol"></param>
  /// <param name="configReqDto"></param>
  /// <param name="currentAssets"></param>
  /// <returns></returns>
  public Task<IEnumerable<AbsAllocReqDto>?> BalancedAbsAllocs(string quoteSymbol, ConfigReqDto configReqDto, List<MarketReqDto>? currentAssets = null);
}