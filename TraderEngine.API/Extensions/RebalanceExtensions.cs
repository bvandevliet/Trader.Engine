using TraderEngine.API.Exchanges;
using TraderEngine.API.Extensions;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Helpers;
using TraderEngine.Common.Models;

namespace TraderEngine.API.Extensions;

public static partial class Trader
{
  /// <summary>
  /// A task that will complete when verified that the given <paramref name="order"/> has ended.
  /// If the given order is not completed within given amount of <paramref name="checks"/>, it will be cancelled.
  /// Every new check is performed one second after the previous has been resolved.
  /// </summary>
  /// <param name="this"></param>
  /// <param name="order"></param>
  /// <param name="checks"></param>
  /// <returns>Completes when verified that the given <paramref name="order"/> has ended.</returns>
  public static async Task<OrderDto> VerifyOrderEnded(this IExchange @this, OrderDto order, int checks = 60)
  {
    while (
      checks > 0 &&
      order.Id != null &&
      order.Status
        is not Common.Enums.OrderStatus.Canceled
        and not Common.Enums.OrderStatus.Expired
        and not Common.Enums.OrderStatus.Rejected
        and not Common.Enums.OrderStatus.Filled)
    {
      await Task.Delay(1000);

      order = await @this.GetOrder(order.Id, order.Market) ?? order;

      checks--;
    }

    if (checks == 0)
    {
      //order = await @thsis.CancelOrder(order.Id!, order.Market) ?? order;
    }

    return order;
  }

  /// <summary>
  /// Get a buy order request dto.
  /// </summary>
  /// <param name="this"></param>
  /// <param name="curAlloc"></param>
  /// <param name="amountQuote"></param>
  /// <returns></returns>
  public static OrderReqDto ConstructBuyOrder(this IExchange @this, Allocation curAlloc, decimal amountQuote)
  {
    return new OrderReqDto()
    {
      Market = curAlloc.Market,
      Side = Common.Enums.OrderSide.Buy,
      Type = Common.Enums.OrderType.Market,
      AmountQuote = Math.Floor(amountQuote * 100) / 100,
    };
  }

  /// <summary>
  /// Get a sell order request dto.
  /// </summary>
  /// <param name="this"></param>
  /// <param name="curAlloc"></param>
  /// <param name="amountQuote"></param>
  /// <returns></returns>
  public static OrderReqDto ConstructSellOrder(this IExchange @this, Allocation curAlloc, decimal amountQuote)
  {
    var order = new OrderReqDto()
    {
      Market = curAlloc.Market,
      Side = Common.Enums.OrderSide.Sell,
      Type = Common.Enums.OrderType.Market,
    };

    // Prevent dust.
    if (curAlloc.AmountQuote - amountQuote <= @this.MinOrderSizeInQuote)
    {
      order.Amount = curAlloc.Amount;
    }
    else
    {
      order.AmountQuote = Math.Ceiling(amountQuote * 100) / 100;
    }

    return order;
  }

  /// <summary>
  /// Sell pieces of oversized <see cref="Allocation"/>s in order for those to meet <paramref name="newAbsAllocs"/>.
  /// Completes when verified that all triggered sell orders are ended.
  /// </summary>
  /// <param name="this"></param>
  /// <param name="newAbsAllocs"></param>
  /// <param name="curBalance"></param>
  /// <returns></returns>
  internal static async Task<OrderDto[]> SellOveragesAndVerify(
    this IExchange @this, IEnumerable<AbsAllocReqDto> newAbsAllocs, Balance? curBalance = null)
  {
    // Fetch balance if not provided.
    curBalance ??= await @this.GetBalance();

    if (null == curBalance)
    {
      return Array.Empty<OrderDto>();
    }

    // Get enumerable since we're iterating it just once.
    var allocDiffs = RebalanceHelpers.GetAllocationQuoteDiffs(newAbsAllocs, curBalance);

    return await @this.SellOveragesAndVerify(allocDiffs);
  }

  /// <summary>
  /// Sell pieces of oversized <see cref="Allocation"/>s as defined in <paramref name="allocDiffs"/>.
  /// Completes when verified that all triggered sell orders are ended.
  /// </summary>
  /// <param name="this"></param>
  /// <param name="allocDiffs"></param>
  /// <returns></returns>
  internal static async Task<OrderDto[]> SellOveragesAndVerify(
    this IExchange @this, IEnumerable<AllocDiffReqDto> allocDiffs)
  {
    // The sell task loop ..
    IEnumerable<Task<OrderDto>> sellTasks =
      allocDiffs

      // We can't sell quote currency for quote currency.
      .Where(allocDiff => !allocDiff.Market.BaseSymbol.Equals(@this.QuoteSymbol))

      // Positive quote differences refer to oversized allocations,
      // and check if reached minimum order size.
      .Where(allocDiff => allocDiff.AmountQuoteDiff >= @this.MinOrderSizeInQuote)

      // Sell ..
      .Select(allocDiff =>
        @this.NewOrder(@this.ConstructSellOrder(
          new Allocation(allocDiff.Market, allocDiff.Price, allocDiff.Amount),
          allocDiff.AmountQuoteDiff))

        // Continue to verify sell order ended, within same task to optimize performance.
        .ContinueWith(sellTask => @this.VerifyOrderEnded(sellTask.Result)).Unwrap());

    return await Task.WhenAll(sellTasks);
  }

  /// <summary>
  /// Buy to increase undersized <see cref="Allocation"/>s in order for those to meet <paramref name="newAbsAllocs"/>.
  /// <see cref="Allocation"/> differences are scaled relative to available quote currency.
  /// Completes when all triggered buy orders are posted.
  /// </summary>
  /// <param name="this"></param>
  /// <param name="newAbsAllocs"></param>
  /// <returns></returns>
  internal static async Task<OrderDto[]> BuyUnderagesAndVerify(
    this IExchange @this, IEnumerable<AbsAllocReqDto> newAbsAllocs, Balance? curBalance = null)
  {
    // Fetch balance if not provided.
    curBalance ??= await @this.GetBalance();

    if (null == curBalance)
    {
      return Array.Empty<OrderDto>();
    }

    // Initialize quote diff List,
    // being filled using a multi-purpose foreach to eliminate redundant iterations.
    List<AllocDiffReqDto> allocDiffs = new();

    // Absolute sum of all negative quote differences,
    // being summed up using a multi-purpose foreach to eliminate redundant iterations.
    decimal totalBuy = 0;

    // Multi-purpose foreach to eliminate redundant iterations.
    foreach (var allocDiff in RebalanceHelpers.GetAllocationQuoteDiffs(newAbsAllocs, curBalance))
    {
      // Negative quote differences refer to undersized allocations.
      if (allocDiff.AmountQuoteDiff <= -@this.MinOrderSizeInQuote)
      {
        // Add to absolute sum of all negative quote differences.
        totalBuy -= allocDiff.AmountQuoteDiff;

        // We can't buy quote currency with quote currency.
        if (!allocDiff.Market.BaseSymbol.Equals(@this.QuoteSymbol))
        {
          // Add to quote diff List.
          allocDiffs.Add(allocDiff);
        }
      }
    }

    // Multiplication ratio to avoid potentially oversized buy order sizes.
    decimal ratio = totalBuy == 0 ? 0 : Math.Min(totalBuy, curBalance.AmountQuoteAvailable) / totalBuy;

    // The buy task loop, diffs are already filtered ..
    IEnumerable<Task<OrderDto>> buyTasks =
      allocDiffs

      // Scale to avoid potentially oversized buy order sizes.
      .Select(allocDiff => { allocDiff.AmountQuoteDiff *= ratio; return allocDiff; })

      // Negative quote differences refer to undersized allocations,
      // and check if reached minimum order size.
      .Where(allocDiff => allocDiff.AmountQuoteDiff <= -@this.MinOrderSizeInQuote)

      // Buy ..
      .Select(allocDiff =>
         @this.NewOrder(@this.ConstructBuyOrder(
           new Allocation(allocDiff.Market, allocDiff.Price, allocDiff.Amount),
           Math.Abs(allocDiff.AmountQuoteDiff)))

        // Continue to verify buy order ended, within same task to optimize performance.
        .ContinueWith(buyTask => @this.VerifyOrderEnded(buyTask.Result)).Unwrap());

    return await Task.WhenAll(buyTasks);
  }

  /// <summary>
  /// Asynchronously performs a portfolio rebalance.
  /// </summary>
  /// <param name="this"></param>
  /// <param name="newAbsAllocs"></param>
  /// <param name="curBalance"></param>
  public static async Task<OrderDto[]> Rebalance(
    this IExchange @this,
    IEnumerable<AbsAllocReqDto> newAbsAllocs,
    Balance? curBalance = null)
  {
    // Clear the path ..
    await @this.CancelAllOpenOrders();

    // Sell pieces of oversized allocations first,
    // so we have sufficient quote currency available to buy with.
    var sellResults = await @this.SellOveragesAndVerify(newAbsAllocs, curBalance);

    // Then buy to increase undersized allocations.
    var buyResults = await @this.BuyUnderagesAndVerify(newAbsAllocs);

    // Combined results.
    var orderResults = new OrderDto[sellResults.Length + buyResults.Length];

    Array.Copy(sellResults, 0, orderResults, 0, sellResults.Length);
    Array.Copy(buyResults, 0, orderResults, sellResults.Length, buyResults.Length);

    return orderResults;
  }

  /// <summary>
  /// Asynchronously performs a portfolio rebalance.
  /// </summary>
  /// <param name="this"></param>
  /// <param name="newAbsAllocs"></param>
  /// <param name="allocDiffs"></param>
  public static async Task<OrderDto[]> Rebalance(
    this IExchange @this,
    IEnumerable<AbsAllocReqDto> newAbsAllocs,
    IEnumerable<AllocDiffReqDto> allocDiffs)
  {
    // Clear the path ..
    await @this.CancelAllOpenOrders();

    // Sell pieces of oversized allocations first,
    // so we have sufficient quote currency available to buy with.
    var sellResults = await @this.SellOveragesAndVerify(allocDiffs);

    // Then buy to increase undersized allocations.
    var buyResults = await @this.BuyUnderagesAndVerify(newAbsAllocs);

    // Combined results.
    var orderResults = new OrderDto[sellResults.Length + buyResults.Length];

    Array.Copy(sellResults, 0, orderResults, 0, sellResults.Length);
    Array.Copy(buyResults, 0, orderResults, sellResults.Length, buyResults.Length);

    return orderResults;
  }
}