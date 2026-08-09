namespace TraderEngine.API.DTOs.Bitvavo.Response;

public class BitvavoTickerBookDto
{
  /// <summary>
  /// Market that was queried.
  /// </summary>
  public string Market { get; set; } = null!;

  /// <summary>
  /// Highest price a buyer is currently willing to pay.
  /// </summary>
  public string? Bid { get; set; }

  /// <summary>
  /// Quantity available at <see cref="Bid"/>.
  /// </summary>
  public string? BidSize { get; set; }

  /// <summary>
  /// Lowest price a seller is currently willing to accept.
  /// </summary>
  public string? Ask { get; set; }

  /// <summary>
  /// Quantity available at <see cref="Ask"/>.
  /// </summary>
  public string? AskSize { get; set; }
}
