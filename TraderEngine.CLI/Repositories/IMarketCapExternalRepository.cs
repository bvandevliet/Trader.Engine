using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;

namespace TraderEngine.CLI.Repositories;

/// <summary>
/// Retrieves latest market cap data from external service.
/// </summary>
internal interface IMarketCapExternalRepository
{
  /// <summary>
  /// Get current market cap data for the specified market.
  /// </summary>
  /// <param name="market">Market for which to get current market cap data.</param>
  /// <returns>Market cap data.</returns>
  Task<MarketCapDataDto> GetMarketCap(MarketReqDto market);

  /// <summary>
  /// Get the latest market cap data of the specified amount of top ranked base currencies for the specified quote currency.
  /// </summary>
  /// <param name="quoteSymbol"></param>
  /// <param name="count"></param>
  /// <returns></returns>
  Task<IEnumerable<MarketCapDataDto>> ListLatest(string quoteSymbol, int count = 100);
}