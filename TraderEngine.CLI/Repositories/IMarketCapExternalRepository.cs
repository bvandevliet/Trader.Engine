using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;

namespace TraderEngine.CLI.Repositories;

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
  public Task<MarketCapDataDto> GetMarketCap(MarketReqDto market);

  /// <summary>
  /// Get the latest market cap data of the top 150 ranked base currencies for the specified quote currency.
  /// </summary>
  /// <param name="quoteSymbol"></param>
  /// <returns></returns>
  public Task<IEnumerable<MarketCapDataDto>> ListLatest(string quoteSymbol);
}