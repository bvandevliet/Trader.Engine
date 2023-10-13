using System.ComponentModel.DataAnnotations;
using TraderEngine.Common.DTOs.API.Request;

namespace TraderEngine.Common.DTOs.API.Response;

public class AllocationDto
{
  [Required]
  public MarketReqDto Market { get; set; } = null!;

  [Required]
  public decimal Price { get; set; }

  [Required]
  public decimal Amount { get; set; }

  public decimal AmountQuote { get; set; }
}