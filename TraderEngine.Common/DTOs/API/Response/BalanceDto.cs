using TraderEngine.Common.Models;

namespace TraderEngine.Common.DTOs.API.Response;

/// <summary>
/// Represents a portfolio balance, containing relative asset allocations.
/// </summary>
public class BalanceDto
{
  /// <summary>
  /// The quote currency on which this balance instance is based.
  /// </summary>
  public string QuoteSymbol { get; set; } = null!;

  /// <summary>
  /// Collection of <see cref="Allocation"/> instances.
  /// </summary>
  public List<AllocationDto> Allocations { get; set; } = new();

  /// <summary>
  /// Amount of quote currency available.
  /// </summary>
  public decimal AmountQuoteAvailable { get; set; }

  /// <summary>
  /// Total value of balance in quote currency.
  /// </summary>
  public decimal AmountQuoteTotal { get; set; }
}