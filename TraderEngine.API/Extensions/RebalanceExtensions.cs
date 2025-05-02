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
  /// Try update all unknown market statuses in <paramref name="absAllocs"/>.
  /// </summary>
  /// <param name="exchange"></param>
  /// <param name="absAllocs"></param>
  /// <returns>Collection of updated <see cref="AbsAllocReqDto"/>s.</returns></returns>
  public static Task<AbsAllocReqDto[]> FetchMarketStatus(this IExchange exchange, IEnumerable<AbsAllocReqDto> absAllocs)
  {
    // Get market data for all assets and update market status.
    var allocsMarketDataTasks = absAllocs.Select(async absAlloc =>
    {
      if (absAlloc.MarketStatus == MarketStatus.Unknown)
      {
        var marketDto = new MarketReqDto(exchange.QuoteSymbol, absAlloc.Market.BaseSymbol);

        var marketData = await exchange.GetMarket(marketDto);

        absAlloc.MarketStatus = marketData?.Status ?? MarketStatus.Unknown;
      }

      return absAlloc;
    });

    // Wait for all tasks to complete.
    return Task.WhenAll(allocsMarketDataTasks);
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
    // Absolute asset allocations to be used for rebalancing.
    List<AbsAllocReqDto> newAbsAllocsList = new();

    // Sum of all absolute allocation values.
    decimal totalAbsAlloc =
      newAbsAllocs

      // Filter for tradable assets.
      .Where(absAlloc => absAlloc.MarketStatus is MarketStatus.Trading
      || null != curBalance.GetAllocation(absAlloc.Market.BaseSymbol))

      // Filter for quote currency.
      .Where(absAlloc => absAlloc.Market.QuoteSymbol.Equals(@this.QuoteSymbol))

      // Sum of all absolute allocation values.
      .Sum(absAlloc =>
      {
        newAbsAllocsList.Add(absAlloc);

        return absAlloc.AbsAlloc;
      });

    // Relative quote allocation (including takeout).
    decimal quoteRelAlloc = curBalance.AmountQuoteTotal == 0 ? 0 : Math.Max(0, Math.Min(1,
      config.QuoteTakeout / curBalance.AmountQuoteTotal + config.QuoteAllocation / 100));

    // Scale total sum of absolute allocation values to account for relative quote allocation.
    decimal div = 1 - quoteRelAlloc;
    if (div == 0)
      totalAbsAlloc = 0;
    else
      totalAbsAlloc /= div;

    // NOTE: No need to add quote allocation, since it's already been accounted for in the total abs value.
    //newAbsAllocsList.Add(new AbsAllocReqDto(@this.QuoteSymbol, totalAbsAlloc * quoteRelAlloc));

    // Loop through current allocations and determine quote diffs.
    foreach (var curAlloc in curBalance.Allocations)
    {
      // Find associated absolute allocation.
      var newAbsAlloc = newAbsAllocsList
        .FindAndRemove(absAlloc => absAlloc.Market.Equals(curAlloc.Market));

      // Skip if not tradable.
      if (null != newAbsAlloc && newAbsAlloc.MarketStatus is not MarketStatus.Trading)
      {
        continue;
      }

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
      // Skip if not tradable.
      if (newAbsAlloc.MarketStatus is not MarketStatus.Trading)
      {
        continue;
      }

      // Determine relative allocation.
      decimal relAlloc = totalAbsAlloc == 0 ? 0 : newAbsAlloc.AbsAlloc / totalAbsAlloc;

      // Determine new quote amount.
      decimal newAmountQuote = relAlloc * curBalance.AmountQuoteTotal;

      yield return new AllocDiffReqDto(
        newAbsAlloc.Market,
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
    this IExchange @this, IEnumerable<AbsAllocReqDto> newAbsAllocs, string source, ConfigReqDto config, Balance? curBalance = null)
  {
    if (null == curBalance)
    {
      var curBalanceResult = await @this.GetBalance();
      curBalance = curBalanceResult.Value!;
    }

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
          Source = source,
        };

        // Prevent dust.
        if (allocDiff.AmountQuote - allocDiff.AmountQuoteDiff < @this.MinOrderSizeInQuote)
        {
          // Honor decimals precision for the amount of this asset.
          var assetData = @this.GetAsset(allocDiff.Market.BaseSymbol).GetAwaiter().GetResult();
          int decimals = assetData?.Decimals ?? 8;

          order.Amount = Math.Floor(allocDiff.Amount * (decimal)Math.Pow(10, decimals)) / (decimal)Math.Pow(10, decimals);
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
        .ContinueWith(sellTask => @this.VerifyOrderEnded(sellTask.Result.Value!, true)).Unwrap()));
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
    this IExchange @this, IEnumerable<AbsAllocReqDto> newAbsAllocs, string source, ConfigReqDto config, Balance? curBalance = null)
  {
    if (null == curBalance)
    {
      var curBalanceResult = await @this.GetBalance();
      curBalance = curBalanceResult.Value!;
    }

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
        Source = source,
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
    if (null == curBalance)
    {
      var curBalanceResult = await @this.GetBalance();
      curBalance = curBalanceResult.Value!;
    }

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
        .ContinueWith(buyTask => @this.VerifyOrderEnded(buyTask.Result.Value!, false)).Unwrap()));
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
    Balance? curBalance = null,
    string source = "Trader.Automation")
  {
    // Clear the path ..
    _ = await @this.CancelAllOpenOrders();

    // Filter for assets that are potentially tradable.
    var absAllocsUpdated = await @this.FetchMarketStatus(newAbsAllocs);
    //var absAllocsTradable = GetTradableAssets(absAllocsUpdated).ToList();

    // Sell pieces of oversized allocations first,
    // so we have sufficient quote currency available to buy with.
    var sellResults = await @this.SellOveragesAndVerify(absAllocsUpdated, source, config, curBalance);

    // Then buy to increase undersized allocations.
    var buyResults = await @this.BuyUnderagesAndVerify(absAllocsUpdated, source, config);

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