using System.ComponentModel.DataAnnotations;

namespace TraderEngine.Common.DTOs.Request;

public class AllocationDto
{
  [Required]
  public MarketDto Market { get; set; } = null!;

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
    MarketDto market,
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