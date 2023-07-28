using TraderEngine.Common.DTOs.Request;
using TraderEngine.Common.DTOs.Response;

namespace TraderEngine.Common.Services;

/// <summary>
/// Interacts with the internal market cap database.
/// </summary>
public interface IMarketCapInternalRepository
{
  /// <summary>
  /// Saves a market cap object to the database.
  /// </summary>
  /// <param name="marketCap"></param>
  /// <returns></returns>
  Task Insert(MarketCapDataDto marketCap);

  /// <summary>
  /// Saves multiple market cap objects to the database.
  /// </summary>
  /// <param name="marketCaps"></param>
  /// <returns></returns>
  Task InsertMany(IEnumerable<MarketCapDataDto> marketCaps);

  /// <summary>
  /// Get all historical market cap data for the specified market within given amount of days ago.
  /// </summary>
  /// <param name="market"></param>
  /// <param name="days"></param>
  /// <returns></returns>
  Task<IEnumerable<MarketCapDataDto>> ListHistorical(MarketReqDto market, int days = 21);

  /// <summary>
  /// Get all historical market cap data of top ranked base currencies for the specified quote currency within given amount of days ago.
  /// </summary>
  /// <param name="quoteSymbol"></param>
  /// <param name="days"></param>
  /// <returns></returns>
  IAsyncEnumerable<IEnumerable<MarketCapDataDto>> ListHistoricalMany(string quoteSymbol, int days = 21);
}