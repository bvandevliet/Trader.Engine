using System.ComponentModel.DataAnnotations;
using TraderEngine.Common.DTOs.API.Response;

namespace TraderEngine.Common.DTOs.API.Request;

public class AllocDiffReqDto : AllocationDto
{
  [Required]
  public decimal AmountQuoteDiff { get; set; }

  public AllocDiffReqDto()
  {
  }

  public AllocDiffReqDto(
    MarketReqDto market,
    decimal price,
    decimal amount,
    decimal amountQuoteDiff)
  {
    Market = market;
    Price = price;
    Amount = amount;
    AmountQuote = price * amount;
    AmountQuoteDiff = amountQuoteDiff;
  }
}