namespace TraderEngine.API.DTOs.Bitvavo.Response;

public class BitvavoTickerPriceDto
{
  /// <summary>
  /// Market that was queried.
  /// </summary>
  public string Market { get; set; } = null!;

  /// <summary>
  /// The last trade price.
  /// </summary>
  public decimal Price { get; set; }
}