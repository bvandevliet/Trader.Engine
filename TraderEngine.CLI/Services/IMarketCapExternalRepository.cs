using TraderEngine.Common.DTOs.Request;
using TraderEngine.Common.DTOs.Response;

namespace TraderEngine.CLI.Services;

/// <summary>
/// Retrieves latest market cap data from external service.
/// </summary>
public interface IMarketCapExternalRepository
{
  /// <summary>
  /// Get current market cap data for the specified market.
  /// </summary>
  /// <param name="market">Market for which to get current market cap data.</param>
  /// <returns>Market cap data.</returns>
  Task<MarketCapData> GetMarketCap(MarketDto market);

  /// <summary>
  /// Get the latest market cap data of the specified amount of top ranked base currencies for the specified quote currency.
  /// </summary>
  /// <param name="quoteSymbol"></param>
  /// <param name="count"></param>
  /// <returns></returns>
  Task<IEnumerable<MarketCapData>> ListLatest(string quoteSymbol, int count = 100);
}