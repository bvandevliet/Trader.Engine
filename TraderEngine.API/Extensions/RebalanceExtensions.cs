using TraderEngine.API.Exchanges;
using TraderEngine.API.Extensions;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Models;

namespace TraderEngine.API.Extensions;

public static partial class Trader
{
  /// <summary>
  /// Get current deviation in quote currency when comparing absolute new allocations in
  /// <paramref name="newAbsAllocs"/> against current allocations in <paramref name="curBalance"/>.
  /// </summary>
  /// <param name="newAbsAllocs"></param>
  /// <param name="curBalance"></param>
  /// <returns>Collection of current <see cref="Allocation"/>s and their deviation in quote currency.</returns>
  public static IEnumerable<KeyValuePair<Allocation, decimal>> GetAllocationQuoteDiffs(IEnumerable<AbsAllocReqDto> newAbsAllocs, Balance curBalance)
  {
    // Initialize absolute asset allocation List,
    // being filled using a multi-purpose foreach to eliminate redundant iterations.
    List<AbsAllocReqDto> newAbsAllocsList = new();

    // Sum of all absolute allocation values.
    // being summed up using a multi-purpose foreach to eliminate redundant iterations.
    decimal totalAbsAlloc = 0;

    // Multi-purpose foreach to eliminate redundant iterations.
    foreach (AbsAllocReqDto absAssetAlloc in newAbsAllocs)
    {
      // Add to sum of all absolute allocation values.
      totalAbsAlloc += absAssetAlloc.AbsAlloc;

      // Add to absolute asset allocation List.
      newAbsAllocsList.Add(absAssetAlloc);
    }

    // Loop through current allocations and determine quote diffs.
    foreach (Allocation curAlloc in curBalance.Allocations)
    {
      // Find associated absolute allocation.
      decimal absAlloc =
        newAbsAllocsList.Find(absAssetAlloc => absAssetAlloc.BaseSymbol.Equals(curAlloc.Market.BaseSymbol))?.AbsAlloc ?? 0;

      // Determine relative allocation.
      decimal relAlloc = totalAbsAlloc == 0 ? 0 : absAlloc / totalAbsAlloc;

      // Determine new quote amount.
      decimal newAmountQuote = relAlloc * curBalance.AmountQuoteTotal;

      yield return new KeyValuePair<Allocation, decimal>(curAlloc, curAlloc.AmountQuote - newAmountQuote);
    }

    // Loop through absolute asset allocations and determine yet missing quote diffs.
    foreach (AbsAllocReqDto absAssetAlloc in newAbsAllocsList)
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

      yield return new KeyValuePair<Allocation, decimal>(curAlloc, -newAmountQuote);
    }
  }

  /// <summary>
  /// A task that will complete when verified that the given <paramref name="order"/> is ended.
  /// If the given order is not completed within given amount of <paramref name="checks"/>, it will be cancelled.
  /// Every new check is performed one second after the previous has been resolved.
  /// </summary>
  /// <param name="this"></param>
  /// <param name="order"></param>
  /// <param name="checks"></param>
  /// <returns>Completes when verified that the given <paramref name="order"/> is ended.</returns>
  public static async Task<OrderDto> VerifyOrderEnded(this IExchange @this, OrderDto order, int checks = 60)
  {
    while (
      checks > 0 &&
      order.Id != null &&
      !order.Status.HasFlag(
        Common.Enums.OrderStatus.Canceled |
        Common.Enums.OrderStatus.Expired |
        Common.Enums.OrderStatus.Rejected |
        Common.Enums.OrderStatus.Filled))
    {
      await Task.Delay(1000);

      order = await @this.GetOrder(order.Id!, order.Market) ?? order;

      checks--;
    }

    if (checks == 0)
    {
      order = await @this.CancelOrder(order.Id!, order.Market) ?? order;
    }

    return order;
  }

  /// <summary>
  /// Get a buy order object including the expected fee.
  /// </summary>
  /// <param name="this"></param>
  /// <param name="curAlloc"></param>
  /// <param name="amountQuote"></param>
  /// <returns></returns>
  public static OrderReqDto ConstructBuyOrder(this IExchange @this, Allocation curAlloc, decimal amountQuote)
  {
    return new OrderReqDto(
      curAlloc.Market,
      Common.Enums.OrderSide.Buy,
      Common.Enums.OrderType.Market)
    {
      AmountQuote = amountQuote,
    };
  }

  /// <summary>
  /// Get a sell order object including the expected fee.
  /// </summary>
  /// <param name="this"></param>
  /// <param name="curAlloc"></param>
  /// <param name="amountQuote"></param>
  /// <returns></returns>
  public static OrderReqDto ConstructSellOrder(this IExchange @this, Allocation curAlloc, decimal amountQuote)
  {
    var order = new OrderReqDto(
      curAlloc.Market,
      Common.Enums.OrderSide.Sell,
      Common.Enums.OrderType.Market);

    // Prevent dust.
    if (curAlloc.AmountQuote - amountQuote <= @this.MinimumOrderSize)
    {
      order.Amount = curAlloc.Amount;
    }
    else
    {
      order.AmountQuote = amountQuote;
    }

    return order;
  }

  /// <summary>
  /// Sell pieces of oversized <see cref="Allocation"/>s as defined in <paramref name="allocQuoteDiffs"/>.
  /// Completes when verified that all triggered sell orders are ended.
  /// </summary>
  /// <param name="this"></param>
  /// <param name="allocQuoteDiffs"></param>
  /// <returns></returns>
  public static async Task<OrderDto[]> SellOveragesAndVerify(
    this IExchange @this, IEnumerable<KeyValuePair<Allocation, decimal>> allocQuoteDiffs)
  {
    var sellTasks = new List<Task<OrderDto>>();

    // The sell task loop ..
    foreach (KeyValuePair<Allocation, decimal> allocQuoteDiff in allocQuoteDiffs)
    {
      if (allocQuoteDiff.Key.Market.BaseSymbol.Equals(@this.QuoteSymbol))
      {
        // We can't sell quote currency for quote currency.
        continue;
      }

      // Positive quote differences refer to oversized allocations,
      // and check if reached minimum order size.
      if (allocQuoteDiff.Value >= @this.MinimumOrderSize)
      {
        // Sell ..
        sellTasks.Add(@this.NewOrder(@this.ConstructSellOrder(allocQuoteDiff.Key, allocQuoteDiff.Value))
          // Continue to verify sell order ended, within same task to optimize performance.
          .ContinueWith(sellTask => @this.VerifyOrderEnded(sellTask.Result)).Unwrap());
      }
    }

    return await Task.WhenAll(sellTasks);
  }

  /// <summary>
  /// Sell pieces of oversized <see cref="Allocation"/>s in order for those to meet <paramref name="newAbsAllocs"/>.
  /// Completes when verified that all triggered sell orders are ended.
  /// </summary>
  /// <param name="this"></param>
  /// <param name="newAbsAllocs"></param>
  /// <param name="curBalance"></param>
  /// <returns></returns>
  public static async Task<OrderDto[]> SellOveragesAndVerify(
    this IExchange @this, IEnumerable<AbsAllocReqDto> newAbsAllocs, Balance? curBalance = null)
  {
    // Fetch balance if not provided.
    curBalance ??= await @this.GetBalance();

    // Get enumerable since we're iterating it just once.
    IEnumerable<KeyValuePair<Allocation, decimal>> allocQuoteDiffs = GetAllocationQuoteDiffs(newAbsAllocs, curBalance);

    return await @this.SellOveragesAndVerify(allocQuoteDiffs);
  }

  /// <summary>
  /// Buy to increase undersized <see cref="Allocation"/>s in order for those to meet <paramref name="newAbsAllocs"/>.
  /// <see cref="Allocation"/> differences are scaled relative to available quote currency.
  /// Completes when all triggered buy orders are posted.
  /// </summary>
  /// <param name="this"></param>
  /// <param name="newAbsAllocs"></param>
  /// <returns></returns>
  public static async Task<OrderDto[]> BuyUnderages(
    this IExchange @this, IEnumerable<AbsAllocReqDto> newAbsAllocs, Balance? curBalance = null)
  {
    // Fetch balance if not provided.
    curBalance ??= await @this.GetBalance();

    // Initialize quote diff List,
    // being filled using a multi-purpose foreach to eliminate redundant iterations.
    List<KeyValuePair<Allocation, decimal>> allocQuoteDiffs = new();

    // Absolute sum of all negative quote differences,
    // being summed up using a multi-purpose foreach to eliminate redundant iterations.
    decimal totalBuy = 0;

    // Multi-purpose foreach to eliminate redundant iterations.
    foreach (KeyValuePair<Allocation, decimal> allocQuoteDiff in GetAllocationQuoteDiffs(newAbsAllocs, curBalance))
    {
      // Negative quote differences refer to undersized allocations.
      if (allocQuoteDiff.Value < 0)
      {
        // Add to absolute sum of all negative quote differences.
        totalBuy -= allocQuoteDiff.Value;

        // We can't buy quote currency with quote currency.
        if (!allocQuoteDiff.Key.Market.BaseSymbol.Equals(@this.QuoteSymbol))
        {
          // Add to quote diff List.
          allocQuoteDiffs.Add(allocQuoteDiff);
        }
      }
    }

    // Multiplication ratio to avoid potentially oversized buy order sizes.
    decimal ratio = totalBuy == 0 ? 0 : Math.Min(totalBuy, curBalance.AmountQuote) / totalBuy;

    var buyTasks = new List<Task<OrderDto>>();

    // The buy task loop, diffs are already filtered ..
    foreach (KeyValuePair<Allocation, decimal> allocQuoteDiff in allocQuoteDiffs)
    {
      // Scale to avoid potentially oversized buy order sizes.
      // First check eligibility as it is less expensive operation than the multiplication operation.
      decimal amountQuote =
        allocQuoteDiff.Value <= -@this.MinimumOrderSize ? ratio * allocQuoteDiff.Value : 0;

      // Negative quote differences refer to undersized allocations,
      // and check if reached minimum order size.
      if (amountQuote <= -@this.MinimumOrderSize)
      {
        // Buy ..
        buyTasks.Add(@this.NewOrder(@this.ConstructBuyOrder(allocQuoteDiff.Key, Math.Abs(amountQuote))));
      }
    }

    return await Task.WhenAll(buyTasks);
  }

  /// <summary>
  /// Asynchronously performs a portfolio rebalance.
  /// </summary>
  /// <param name="this"></param>
  /// <param name="newAbsAllocs"></param>
  /// <param name="allocQuoteDiffs"></param>
  public static async Task<IEnumerable<OrderDto>> Rebalance(
    this IExchange @this,
    IEnumerable<AbsAllocReqDto> newAbsAllocs,
    IEnumerable<KeyValuePair<Allocation, decimal>>? allocQuoteDiffs = null)
  {
    // Clear the path ..
    await @this.CancelAllOpenOrders();

    // Sell pieces of oversized allocations first,
    // so we have sufficient quote currency available to buy with.
    OrderDto[] sellResults = null != allocQuoteDiffs
      ? await @this.SellOveragesAndVerify(allocQuoteDiffs)
      : await @this.SellOveragesAndVerify(newAbsAllocs);

    // Then buy to increase undersized allocations.
    OrderDto[] buyResults = await @this.BuyUnderages(newAbsAllocs);

    return sellResults.Concat(buyResults);
  }
}