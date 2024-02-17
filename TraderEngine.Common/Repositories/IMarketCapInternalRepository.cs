using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;

namespace TraderEngine.Common.Repositories;

/// <summary>
/// Interacts with the internal market cap database.
/// </summary>
public interface IMarketCapInternalRepository
{
  Task<int> InitDatabase();
  Task<int> CleanupDatabase(int daysRetention = 14);

  /// <summary>
  /// Saves a market cap object to the database.
  /// </summary>
  /// <param name="marketCap"></param>
  /// <returns></returns>
  Task<int> TryInsert(MarketCapDataDto marketCap);

  /// <summary>
  /// Saves multiple market cap objects to the database.
  /// </summary>
  /// <param name="marketCaps"></param>
  /// <returns></returns>
  Task<int> TryInsertMany(IEnumerable<MarketCapDataDto> marketCaps);

  /// <summary>
  /// Get all historical market cap data for the specified <paramref name="market"/> within given amount of <paramref name="hours"/> ago.
  /// </summary>
  /// <param name="market"></param>
  /// <param name="hours"></param>
  /// <returns></returns>
  Task<IEnumerable<MarketCapDataDto>> ListHistorical(MarketReqDto market, int hours = 24);

  /// <summary>
  /// Get all historical market cap data of top ranked base currencies for the specified <paramref name="quoteSymbol"/> within given amount of <paramref name="hours"/> ago.
  /// </summary>
  /// <param name="quoteSymbol"></param>
  /// <param name="hours"></param>
  /// <returns></returns>
  Task<IEnumerable<IEnumerable<MarketCapDataDto>>> ListHistoricalMany(string quoteSymbol, int hours = 24);
}