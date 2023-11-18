using TraderEngine.Common.DTOs.API.Request;

namespace TraderEngine.Common.DTOs.API.Response;

public class AllocationDto
{
  public MarketReqDto Market { get; set; } = null!;

  public decimal Price { get; set; }

  public decimal Amount { get; set; }

  public decimal AmountQuote { get; set; }
}