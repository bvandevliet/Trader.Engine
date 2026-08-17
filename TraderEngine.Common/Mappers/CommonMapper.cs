using Riok.Mapperly.Abstractions;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Models;

namespace TraderEngine.Common.Mappers;

/// <summary>
/// Deep-copies a <see cref="ConfigReqDto"/> — unlike a shallow/<see cref="object.MemberwiseClone"/>
/// copy, <see cref="ConfigReqDto.WeightingOverrides"/>/<see cref="ConfigReqDto.TagsToInclude"/>/
/// <see cref="ConfigReqDto.TagsToIgnore"/> get their own new <c>Dictionary</c>/<c>List</c>
/// instances, so mutating the clone can never affect the original. Kept as its own
/// <c>[Mapper(UseDeepCloning = true)]</c> class, separate from <see cref="CommonMapper"/>, since
/// that setting applies to the whole mapper class and shouldn't affect unrelated mappings there.
/// </summary>
[Mapper(UseDeepCloning = true)]
public static partial class ConfigReqDtoCloner
{
  public static partial ConfigReqDto DeepClone(this ConfigReqDto source);
}

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
