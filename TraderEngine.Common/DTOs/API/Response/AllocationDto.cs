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

  [Required]
  public decimal AmountQuote { get; set; }

  public AllocationDto()
  {
  }

  /// <param name="market"><inheritdoc cref="Market"/></param>
  /// <param name="price"><inheritdoc cref="Price"/></param>
  /// <param name="amount"><inheritdoc cref="Amount"/></param>
  /// <param name="amountQuote"><inheritdoc cref="AmountQuote"/></param>
  public AllocationDto(
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