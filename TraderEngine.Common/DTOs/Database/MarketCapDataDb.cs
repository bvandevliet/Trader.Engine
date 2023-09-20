using System.ComponentModel.DataAnnotations;

namespace TraderEngine.Common.DTOs.Database;

public class MarketCapDataDb
{
  [Required]
  public string QuoteSymbol { get; set; } = null!;

  [Required]
  public string BaseSymbol { get; set; } = null!;

  [Required]
  public double Price { get; set; }

  [Required]
  public double MarketCap { get; set; }

  public string? Tags { get; set; }

  [Required]
  public DateTime Updated { get; set; }
}