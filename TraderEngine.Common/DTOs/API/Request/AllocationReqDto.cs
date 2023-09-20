using System.ComponentModel.DataAnnotations;

namespace TraderEngine.Common.DTOs.API.Request;

public class AllocationReqDto
{
  [Required]
  public MarketReqDto Market { get; set; } = null!;

  [Required]
  public decimal Price { get; set; }

  [Required]
  public decimal Amount { get; set; }

  [Required]
  public decimal AmountQuote { get; set; }

  public AllocationReqDto()
  {
  }

  /// <param name="market"><inheritdoc cref="Market"/></param>
  /// <param name="price"><inheritdoc cref="Price"/></param>
  /// <param name="amount"><inheritdoc cref="Amount"/></param>
  /// <param name="amountQuote"><inheritdoc cref="AmountQuote"/></param>
  public AllocationReqDto(
    MarketReqDto market,
    decimal price,
    decimal amount,
    decimal amountQuote)
  {
    Market = market;
    Price = price;
    Amount = amount;
    AmountQuote = amountQuote;
  }
}