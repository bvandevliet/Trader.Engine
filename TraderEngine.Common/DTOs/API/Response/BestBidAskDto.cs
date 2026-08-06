namespace TraderEngine.Common.DTOs.API.Response;

/// <summary>
/// Best bid/ask prices from an exchange's order book for a single market.
/// </summary>
public class BestBidAskDto
{
  /// <summary>
  /// Highest price a buyer is currently willing to pay.
  /// </summary>
  public decimal Bid { get; set; }

  /// <summary>
  /// Lowest price a seller is currently willing to accept.
  /// </summary>
  public decimal Ask { get; set; }
}
