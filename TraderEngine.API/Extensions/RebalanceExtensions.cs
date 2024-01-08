using TraderEngine.API.Exchanges;
using TraderEngine.API.Extensions;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Extensions;
using TraderEngine.Common.Models;

namespace TraderEngine.API.Extensions;

public static partial class Trader
{
  private class AllocDiffReqDto : AllocationDto
  {
    public decimal AmountQuoteDiff { get; set; }

    public AllocDiffReqDto(
      MarketReqDto market,
      decimal price,
      decimal amount,
      decimal amountQuoteDiff)
    {
      Market = market;
      Price = price;
      Amount = amount;
      AmountQuote = price * amount;
      AmountQuoteDiff = amountQuoteDiff;
    }
  }

  /// <summary>
  /// Get current deviation in quote currency when comparing absolute new allocations in
  /// <paramref name="newAbsAllocs"/> against current allocations in <paramref name="curBalance"/>.
  /// </summary>
  /// <param name="newAbsAllocs"></param>
  /// <param name="config"></param>
  /// <param name="curBalance"></param>
  /// <returns>Collection of current <see cref="Allocation"/>s and their deviation in quote currency.</returns>
  private static IEnumerable<AllocDiffReqDto> GetAllocationQuoteDiffs(
    this IExchange @this, IEnumerable<AbsAllocReqDto> newAbsAllocs, ConfigReqDto config, Balance curBalance)
  {
    // Relative quote allocation (including takeout).
    decimal quoteRelAlloc = Math.Max(0, Math.Min(1,
      config.QuoteTakeout / curBalance.AmountQuoteTotal + config.QuoteAllocation / 100));

    // Sum of all absolute allocation values.
    decimal totalAbsAlloc = 0;

    // Absolute asset allocations to be used for rebalancing.
    var newAbsAllocsList =
      newAbsAllocs

      // Quote allocation is not expected here, but filter it out just in case.
      .Where(absAlloc => !absAlloc.BaseSymbol.Equals(@this.QuoteSymbol))

      // Scale absolute allocation values to include relative quote allocation.
      .Select(absAlloc =>
      {
        totalAbsAlloc += absAlloc.AbsAlloc;

        absAlloc.AbsAlloc *= 1 - quoteRelAlloc;

        return absAlloc;
      })

      // As list since we're using a summed total.
      .ToList();

    // NOTE: No need to add quote allocation, since it's already been accounted for in the total abs value.
    //newAbsAllocsList.Add(new AbsAllocReqDto(@this.QuoteSymbol, totalAbsAlloc * quoteRelAlloc));

    // Loop through current allocations and determine quote diffs.
    foreach (var curAlloc in curBalance.Allocations)
    {
      // Find associated absolute allocation.
      var newAbsAlloc = newAbsAllocsList
        .FindAndRemove(absAlloc => absAlloc.BaseSymbol.Equals(curAlloc.Market.BaseSymbol));

      // Determine relative allocation.
      decimal relAlloc = totalAbsAlloc == 0 || newAbsAlloc == null ? 0 : newAbsAlloc.AbsAlloc / totalAbsAlloc;

      // Determine new quote amount.
      decimal newAmountQuote = relAlloc * curBalance.AmountQuoteTotal;

      yield return new AllocDiffReqDto(
        curAlloc.Market,
        curAlloc.Price,
        curAlloc.Amount,
        curAlloc.AmountQuote - newAmountQuote);
    }

    // Loop through remaining absolute asset allocations and determine yet missing quote diffs.
    foreach (var newAbsAlloc in newAbsAllocsList)
    {
      // Determine relative allocation.
      decimal relAlloc = totalAbsAlloc == 0 ? 0 : newAbsAlloc.AbsAlloc / totalAbsAlloc;

      // Determine new quote amount.
      decimal newAmountQuote = relAlloc * curBalance.AmountQuoteTotal;

      yield return new AllocDiffReqDto(
        new MarketReqDto(curBalance.QuoteSymbol, newAbsAlloc.BaseSymbol),
        0,
        0,
        -newAmountQuote);
    }
  }

  /// <summary>
  /// A task that will complete when verified that the given <paramref name="order"/> has ended.
  /// If the given order is not completed within given amount of <paramref name="checks"/>, it will be cancelled.
  /// Every new check is performed one second after the previous has been resolved.
  /// </summary>
  /// <param name="this"></param>
  /// <param name="order"></param>
  /// <param name="cancel"></param>
  /// <param name="checks"></param>
  /// <returns>Completes when verified that the given <paramref name="order"/> has ended.</returns>
  public static async Task<OrderDto> VerifyOrderEnded(this IExchange @this, OrderDto order, bool cancel = true, int checks = 60)
  {
    while (
      checks > 0 &&
      order.Id != null &&
      !order.HasEnded)
    {
      await Task.Delay(1000);

      order = await @this.GetOrder(order.Id, order.Market) ?? order;

      checks--;
    }

    if (cancel && checks == 0)
    {
      order = await @this.CancelOrder(order.Id!, order.Market) ?? order;
    }

    return order;
  }

  /// <summary>
  /// Sell pieces of oversized <see cref="Allocation"/>s in order for those to meet <paramref name="newAbsAllocs"/>.
  /// Completes when verified that all triggered sell orders are ended.
  /// </summary>
  /// <param name="this"></param>
  /// <param name="newAbsAllocs"></param>
  /// <param name="config"></param>
  /// <param name="curBalance"></param>
  /// <returns></returns>
  private static async Task<OrderDto[]> SellOveragesAndVerify(
    this IExchange @this, IEnumerable<AbsAllocReqDto> newAbsAllocs, ConfigReqDto config, Balance? curBalance = null)
  {
    curBalance ??= await @this.GetBalance();

    var orders =
      @this.GetAllocationQuoteDiffs(newAbsAllocs, config, curBalance)

      // We can't trade quote currency for quote currency.
      .Where(allocDiff => !allocDiff.Market.BaseSymbol.Equals(@this.QuoteSymbol))

      // Positive quote differences refer to oversized allocations.
      .Where(allocDiff => allocDiff.AmountQuoteDiff > 0)

      // Construct sell order.
      .Select(allocDiff =>
      {
        var order = new OrderReqDto()
        {
          Market = allocDiff.Market,
          Side = OrderSide.Sell,
          Type = OrderType.Market,
        };

        // Prevent dust.
        if (allocDiff.AmountQuote - allocDiff.AmountQuoteDiff < @this.MinOrderSizeInQuote)
        {
          order.Amount = allocDiff.Amount;
        }
        else
        {
          order.AmountQuote = allocDiff.AmountQuoteDiff;
        }

        return order;
      });

    return await @this.SellOveragesAndVerify(orders);
  }

  /// <summary>
  /// Sell pieces of oversized <see cref="Allocation"/>s as defined in <paramref name="orders"/>.
  /// Completes when verified that all triggered sell orders are ended.
  /// </summary>
  /// <param name="this"></param>
  /// <param name="orders"></param>
  /// <returns></returns>
  private static async Task<OrderDto[]> SellOveragesAndVerify(
    this IExchange @this, IEnumerable<OrderReqDto> orders)
  {
    // The sell task loop ..
    return await Task.WhenAll(
      orders

      // Filter for sell orders.
      .Where(order => order.Side == OrderSide.Sell)

      // We can't trade quote currency for quote currency.
      .Where(sellOrder => !sellOrder.Market.BaseSymbol.Equals(@this.QuoteSymbol))

      // Check if reached minimum order size.
      .Where(sellOrder => sellOrder.AmountQuote >= @this.MinOrderSizeInQuote || sellOrder.Amount > 0)

      // Round to avoid potentially invalid amount quote.
      .Select(sellOrder =>
      {
        if (sellOrder.AmountQuote is decimal amountQuote)
          sellOrder.AmountQuote = Math.Ceiling(amountQuote * 100) / 100;

        return sellOrder;
      })

      // Sell ..
      .Select(alloc => @this.NewOrder(alloc)

        // Continue to verify sell order ended, within same task to optimize performance.
        .ContinueWith(sellTask => @this.VerifyOrderEnded(sellTask.Result, true)).Unwrap()));
  }

  /// <summary>
  /// Buy to increase undersized <see cref="Allocation"/>s in order for those to meet <paramref name="newAbsAllocs"/>.
  /// Completes when all triggered buy orders are posted.
  /// </summary>
  /// <param name="this"></param>
  /// <param name="newAbsAllocs"></param>
  /// <param name="config"></param>
  /// <param name="curBalance"></param>
  /// <returns></returns>
  private static async Task<OrderDto[]> BuyUnderagesAndVerify(
    this IExchange @this, IEnumerable<AbsAllocReqDto> newAbsAllocs, ConfigReqDto config, Balance? curBalance = null)
  {
    curBalance ??= await @this.GetBalance();

    var orders =
      @this.GetAllocationQuoteDiffs(newAbsAllocs, config, curBalance)

      // We can't trade quote currency for quote currency.
      .Where(allocDiff => !allocDiff.Market.BaseSymbol.Equals(@this.QuoteSymbol))

      // Negative quote differences refer to undersized allocations.
      .Where(allocDiff => allocDiff.AmountQuoteDiff < 0)

      // Construct buy order.
      .Select(allocDiff => new OrderReqDto()
      {
        Market = allocDiff.Market,
        Side = OrderSide.Buy,
        Type = OrderType.Market,
        AmountQuote = Math.Abs(allocDiff.AmountQuoteDiff),
      });

    return await @this.BuyUnderagesAndVerify(orders, curBalance);
  }

  /// <summary>
  /// Sell pieces of oversized <see cref="Allocation"/>s as defined in <paramref name="orders"/>.
  /// Completes when verified that all triggered sell orders are ended.
  /// </summary>
  /// <param name="this"></param>
  /// <param name="orders"></param>
  /// <param name="curBalance"></param>
  /// <returns></returns>
  private static async Task<OrderDto[]> BuyUnderagesAndVerify(
    this IExchange @this, IEnumerable<OrderReqDto> orders, Balance? curBalance = null)
  {
    curBalance ??= await @this.GetBalance();

    List<OrderReqDto> buyOrders = new();

    // Absolute sum of all negative quote differences,
    // using a single multi-purpose enumeration to eliminate redundant enumerations.
    decimal totalBuy =
      orders

      // Filter for buy orders.
      .Where(order => order.Side == OrderSide.Buy)

      // We can't trade quote currency for quote currency.
      .Where(buyOrder => !buyOrder.Market.BaseSymbol.Equals(@this.QuoteSymbol))

      // Check if reached minimum order size.
      .Where(buyOrder => buyOrder.AmountQuote >= @this.MinOrderSizeInQuote)

      // Sum of all negative quote differences.
      .Sum(buyOrder =>
      {
        // Add to list.
        buyOrders.Add(buyOrder);

        return (decimal)buyOrder.AmountQuote!;
      });

    // Multiplication ratio to avoid potentially oversized buy order sizes.
    decimal ratio = totalBuy == 0 ? 0 :
      Math.Min(totalBuy, curBalance.AmountQuoteAvailable) / totalBuy;

    // The buy task loop, diffs are already filtered ..
    return await Task.WhenAll(
      buyOrders

      // Scale to avoid potentially oversized buy order size,
      // and round to avoid potentially invalid amount quote.
      .Select(buyOrder =>
      {
        buyOrder.AmountQuote *= ratio;
        buyOrder.AmountQuote = Math.Floor((decimal)buyOrder.AmountQuote! * 100) / 100;

        return buyOrder;
      })

      // Check if still reached minimum order size.
      .Where(buyOrder => buyOrder.AmountQuote >= @this.MinOrderSizeInQuote)

      // Buy ..
      .Select(buyOrder => @this.NewOrder(buyOrder)

        // Continue to verify buy order ended, within same task to optimize performance.
        .ContinueWith(buyTask => @this.VerifyOrderEnded(buyTask.Result, false)).Unwrap()));
  }

  /// <summary>
  /// Asynchronously performs a portfolio rebalance.
  /// Quote allocation and takeout will be handled.
  /// </summary>
  /// <param name="this"></param>
  /// <param name="config"></param>
  /// <param name="newAbsAllocs"></param>
  /// <param name="curBalance"></param>
  public static async Task<OrderDto[]> Rebalance(
    this IExchange @this,
    ConfigReqDto config,
    IEnumerable<AbsAllocReqDto> newAbsAllocs,
    Balance? curBalance = null)
  {
    curBalance ??= await @this.GetBalance();

    // Clear the path ..
    _ = await @this.CancelAllOpenOrders();

    // Sell pieces of oversized allocations first,
    // so we have sufficient quote currency available to buy with.
    var sellResults = await @this.SellOveragesAndVerify(newAbsAllocs, config, curBalance);

    // Then buy to increase undersized allocations.
    var buyResults = await @this.BuyUnderagesAndVerify(newAbsAllocs, config);

    // Combined results.
    var orderResults = new OrderDto[sellResults.Length + buyResults.Length];

    Array.Copy(sellResults, 0, orderResults, 0, sellResults.Length);
    Array.Copy(buyResults, 0, orderResults, sellResults.Length, buyResults.Length);

    return orderResults;
  }

  /// <summary>
  /// Asynchronously performs a portfolio rebalance.
  /// Just executes the given orders, without any checks.
  /// </summary>
  /// <param name="this"></param>
  /// <param name="orders"></param>
  public static async Task<OrderDto[]> Rebalance(
    this IExchange @this,
    IEnumerable<OrderReqDto> orders)
  {
    // Clear the path ..
    _ = await @this.CancelAllOpenOrders();

    // Sell pieces of oversized allocations first,
    // so we have sufficient quote currency available to buy with.
    var sellResults = await @this.SellOveragesAndVerify(orders);

    // Then buy to increase undersized allocations.
    var buyResults = await @this.BuyUnderagesAndVerify(orders);

    // Combined results.
    var orderResults = new OrderDto[sellResults.Length + buyResults.Length];

    Array.Copy(sellResults, 0, orderResults, 0, sellResults.Length);
    Array.Copy(buyResults, 0, orderResults, sellResults.Length, buyResults.Length);

    return orderResults;
  }
}