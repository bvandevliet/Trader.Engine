namespace TraderEngine.API.DTOs.Bitvavo.Response;

public class BitvavoAllocationDto
{
  /// <summary>
  /// Short version of asset name.
  /// </summary>
  public string Symbol { get; set; } = null!;

  /// <summary>
  /// Balance freely available.
  /// </summary>
  public string Available { get; set; } = null!;

  /// <summary>
  /// Balance currently placed onHold for open orders.
  /// </summary>
  public string InOrder { get; set; } = null!;
}