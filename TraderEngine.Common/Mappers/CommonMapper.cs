using Riok.Mapperly.Abstractions;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Models;

namespace TraderEngine.Common.Mappers;

[Mapper]
public static partial class CommonMapper
{
  // ── Allocation ──────────────────────────────────────────────────────────────

  public static partial AllocationDto MapAllocation(Allocation source);

  // ── Balance ──────────────────────────────────────────────────────────────────

  public static BalanceDto MapBalance(Balance source)
  {
    return new()
    {
      QuoteSymbol = source.QuoteSymbol,
      AmountQuoteAvailable = source.AmountQuoteAvailable,
      AmountQuoteTotal = source.AmountQuoteTotal,
      Allocations = source.Allocations
      .OrderBy(a => !a.Market.BaseSymbol.Equals(source.QuoteSymbol))
      .ThenByDescending(a => a.AmountQuote)
      .Select(MapAllocation)
      .ToList(),
    };
  }
}
