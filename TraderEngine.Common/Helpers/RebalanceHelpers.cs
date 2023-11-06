using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Models;

namespace TraderEngine.Common.Helpers;

public static class RebalanceHelpers
{
  /// <summary>
  /// Get current deviation in quote currency when comparing absolute new allocations in
  /// <paramref name="newAbsAllocs"/> against current allocations in <paramref name="curBalance"/>.
  /// </summary>
  /// <param name="newAbsAllocs"></param>
  /// <param name="curBalance"></param>
  /// <returns>Collection of current <see cref="Allocation"/>s and their deviation in quote currency.</returns>
  // TODO: Make DRY !!
  public static IEnumerable<AllocDiffReqDto> GetAllocationQuoteDiffs(IEnumerable<AbsAllocReqDto> newAbsAllocs, Balance curBalance)
  {
    // Initialize absolute asset allocation List,
    // being filled using a multi-purpose foreach to eliminate redundant iterations.
    List<AbsAllocReqDto> newAbsAllocsList = new();

    // Sum of all absolute allocation values.
    // being summed up using a multi-purpose foreach to eliminate redundant iterations.
    decimal totalAbsAlloc = 0;

    // Multi-purpose foreach to eliminate redundant iterations.
    foreach (var absAssetAlloc in newAbsAllocs)
    {
      // Add to sum of all absolute allocation values.
      totalAbsAlloc += absAssetAlloc.AbsAlloc;

      // Add to absolute asset allocation List.
      newAbsAllocsList.Add(absAssetAlloc);
    }

    // Loop through current allocations and determine quote diffs.
    foreach (var curAlloc in curBalance.Allocations)
    {
      // Find associated absolute allocation.
      decimal absAlloc =
        newAbsAllocsList.Find(absAssetAlloc => absAssetAlloc.BaseSymbol.Equals(curAlloc.Market.BaseSymbol))?.AbsAlloc ?? 0;

      // Determine relative allocation.
      decimal relAlloc = totalAbsAlloc == 0 ? 0 : absAlloc / totalAbsAlloc;

      // Determine new quote amount.
      decimal newAmountQuote = relAlloc * curBalance.AmountQuoteTotal;

      yield return new AllocDiffReqDto(
        curAlloc.Market,
        curAlloc.Price,
        curAlloc.Amount,
        curAlloc.AmountQuote - newAmountQuote);
    }

    // Loop through absolute asset allocations and determine yet missing quote diffs.
    foreach (var absAssetAlloc in newAbsAllocsList)
    {
      if (null != curBalance.GetAllocation(absAssetAlloc.BaseSymbol))
      {
        // Already covered in previous foreach.
        continue;
      }

      // Define current allocation, which is zero here.
      Allocation curAlloc = new(new MarketReqDto(curBalance.QuoteSymbol, absAssetAlloc.BaseSymbol), 0, 0);

      // Determine relative allocation.
      decimal relAlloc = totalAbsAlloc == 0 ? 0 : absAssetAlloc.AbsAlloc / totalAbsAlloc;

      // Determine new quote amount.
      decimal newAmountQuote = relAlloc * curBalance.AmountQuoteTotal;

      yield return new AllocDiffReqDto(
        curAlloc.Market,
        curAlloc.Price,
        curAlloc.Amount,
        -newAmountQuote);
    }
  }

  /// <summary>
  /// Get current deviation in quote currency when comparing absolute new allocations in
  /// <paramref name="newAbsAllocs"/> against current allocations in <paramref name="curBalance"/>.
  /// </summary>
  /// <param name="newAbsAllocs"></param>
  /// <param name="curBalance"></param>
  /// <returns>Collection of current <see cref="Allocation"/>s and their deviation in quote currency.</returns>
  // TODO: Make DRY !!
  public static IEnumerable<AllocDiffReqDto> GetAllocationQuoteDiffs(IEnumerable<AbsAllocReqDto> newAbsAllocs, BalanceDto curBalance)
  {
    // Initialize absolute asset allocation List,
    // being filled using a multi-purpose foreach to eliminate redundant iterations.
    List<AbsAllocReqDto> newAbsAllocsList = new();

    // Sum of all absolute allocation values.
    // being summed up using a multi-purpose foreach to eliminate redundant iterations.
    decimal totalAbsAlloc = 0;

    // Multi-purpose foreach to eliminate redundant iterations.
    foreach (var absAssetAlloc in newAbsAllocs)
    {
      // Add to sum of all absolute allocation values.
      totalAbsAlloc += absAssetAlloc.AbsAlloc;

      // Add to absolute asset allocation List.
      newAbsAllocsList.Add(absAssetAlloc);
    }

    // Loop through current allocations and determine quote diffs.
    foreach (var curAlloc in curBalance.Allocations)
    {
      // Find associated absolute allocation.
      decimal absAlloc =
        newAbsAllocsList.Find(absAssetAlloc => absAssetAlloc.BaseSymbol.Equals(curAlloc.Market.BaseSymbol))?.AbsAlloc ?? 0;

      // Determine relative allocation.
      decimal relAlloc = totalAbsAlloc == 0 ? 0 : absAlloc / totalAbsAlloc;

      // Determine new quote amount.
      decimal newAmountQuote = relAlloc * curBalance.AmountQuoteTotal;

      yield return new AllocDiffReqDto(
        curAlloc.Market,
        curAlloc.Price,
        curAlloc.Amount,
        curAlloc.AmountQuote - newAmountQuote);
    }

    // Loop through absolute asset allocations and determine yet missing quote diffs.
    foreach (var absAssetAlloc in newAbsAllocsList)
    {
      if (null != curBalance.Allocations.Find(alloc => alloc.Market.BaseSymbol.Equals(absAssetAlloc.BaseSymbol)))
      {
        // Already covered in previous foreach.
        continue;
      }

      // Define current allocation, which is zero here.
      Allocation curAlloc = new(new MarketReqDto(curBalance.QuoteSymbol, absAssetAlloc.BaseSymbol), 0, 0);

      // Determine relative allocation.
      decimal relAlloc = totalAbsAlloc == 0 ? 0 : absAssetAlloc.AbsAlloc / totalAbsAlloc;

      // Determine new quote amount.
      decimal newAmountQuote = relAlloc * curBalance.AmountQuoteTotal;

      yield return new AllocDiffReqDto(
        curAlloc.Market,
        curAlloc.Price,
        curAlloc.Amount,
        -newAmountQuote);
    }
  }
}