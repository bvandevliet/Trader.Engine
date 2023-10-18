using System.ComponentModel.DataAnnotations;

namespace TraderEngine.Common.DTOs.API.Request;

public class AllocDiffReqDto
{
  [Required]
  public MarketReqDto Market { get; set; } = null!;

  [Required]
  public decimal Price { get; set; }

  [Required]
  public decimal Amount { get; set; }

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
    AmountQuoteDiff = amountQuoteDiff;
  }
}