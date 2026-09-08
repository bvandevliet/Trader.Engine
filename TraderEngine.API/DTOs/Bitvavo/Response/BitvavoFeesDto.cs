namespace TraderEngine.API.DTOs.Bitvavo.Response;

/// <summary>
/// Response shape of Bitvavo's <c>GET /account/fees</c> endpoint. Fees are determined by the fee
/// category the queried market falls under (e.g. Category A vs. B), so this can differ per market
/// rather than being one flat account-wide rate — querying without a <c>market</c> query parameter
/// returns the account's default Category A fees instead.
/// </summary>
public class BitvavoFeesDto
{
  /// <summary>
  /// The fee tier currently in effect, based on 30-day trading volume.
  /// </summary>
  public string Tier { get; set; } = null!;

  /// <summary>
  /// Trading volume in EUR over the last 30 days.
  /// </summary>
  public string Volume { get; set; } = null!;

  /// <summary>
  /// The fee rate for trades that take liquidity from the order book.
  /// </summary>
  public string Taker { get; set; } = null!;

  /// <summary>
  /// The fee rate for trades that add liquidity to the order book.
  /// </summary>
  public string Maker { get; set; } = null!;
}