using Microsoft.Extensions.Logging;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Exchanges;
using TraderEngine.Common.Extensions;
using TraderEngine.Common.Models;

namespace TraderEngine.Common.Services;

public class RebalancingService : IRebalancingService
{
  private readonly ILogger<RebalancingService> _logger;

  public RebalancingService(ILogger<RebalancingService> logger)
  {
    _logger = logger;
  }

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

  public async Task<AbsAllocReqDto> FetchMarketStatus(IExchange exchange, ExchangeCredentials credentials, AbsAllocReqDto absAlloc)
  {
    // Get market data for the asset and update market status.
    if (absAlloc.MarketStatus == MarketStatus.Unknown)
    {
      var marketDto = new MarketReqDto(exchange.QuoteSymbol, absAlloc.Market.BaseSymbol);

      var marketData = await exchange.GetMarket(credentials, marketDto);

      absAlloc.MarketStatus = marketData?.Status ?? MarketStatus.Unknown;
    }

    return absAlloc;
  }

  public async Task<List<AbsAllocReqDto>> GetTopRankingAllocs(IExchange exchange, ExchangeCredentials credentials, IEnumerable<AbsAllocReqDto> absAllocs, int topRankingCount)
  {
    var absAllocsList = new List<AbsAllocReqDto>();

    foreach (var absAlloc in absAllocs)
    {
      var absAllocUpdated = await FetchMarketStatus(exchange, credentials, absAlloc);

      if (absAlloc.MarketStatus != MarketStatus.Unknown)
      {
        // Expecting the collection to be already ordered by market cap.
        topRankingCount--;
        absAllocsList.Add(absAllocUpdated);
      }

      if (topRankingCount <= 0)
        break;
    }

    return absAllocsList;
  }

  /// <summary>
  /// Get current deviation in quote currency when comparing absolute new allocations in
  /// <paramref name="newAbsAllocs"/> against current allocations in <paramref name="curBalance"/>.
  /// </summary>
  /// <param name="exchange"></param>
  /// <param name="newAbsAllocs"></param>
  /// <param name="config"></param>
  /// <param name="curBalance"></param>
  /// <returns>Collection of current <see cref="Allocation"/>s and their deviation in quote currency.</returns>
  private static IEnumerable<AllocDiffReqDto> GetAllocationQuoteDiffs(
    IExchange exchange, IEnumerable<AbsAllocReqDto> newAbsAllocs, ConfigReqDto config, Balance curBalance)
  {
    // Absolute asset allocations to be used for rebalancing.
    List<AbsAllocReqDto> newAbsAllocsList = new();

    // Sum of all absolute allocation values.
    var totalAbsAlloc =
      newAbsAllocs

      // Filter for tradable assets.
      .Where(absAlloc => absAlloc.MarketStatus is MarketStatus.Trading
      || null != curBalance.GetAllocation(absAlloc.Market.BaseSymbol))

      // Filter for quote currency.
      .Where(absAlloc => absAlloc.Market.QuoteSymbol.Equals(exchange.QuoteSymbol))

      // Sum of all absolute allocation values.
      .Sum(absAlloc =>
      {
        newAbsAllocsList.Add(absAlloc);

        return absAlloc.AbsAlloc;
      });

    // Relative quote allocation (including takeout).
    var quoteRelAlloc = curBalance.AmountQuoteTotal == 0 ? 0 : Math.Max(0, Math.Min(1,
      config.QuoteTakeout / curBalance.AmountQuoteTotal + config.QuoteAllocation / 100));

    // Scale total sum of absolute allocation values to account for relative quote allocation.
    var div = 1 - quoteRelAlloc;
    if (div == 0)
      totalAbsAlloc = 0;
    else
      totalAbsAlloc /= div;

    // NOTE: No need to add quote allocation, since it's already been accounted for in the total abs value.
    //newAbsAllocsList.Add(new AbsAllocReqDto(exchange.QuoteSymbol, totalAbsAlloc * quoteRelAlloc));

    // Loop through current allocations and determine quote diffs.
    foreach (var curAlloc in curBalance.Allocations)
    {
      // Find associated absolute allocation.
      var newAbsAlloc = newAbsAllocsList
        .FindAndRemove(absAlloc => absAlloc.Market.Equals(curAlloc.Market));

      // Skip if not tradable.
      if (null != newAbsAlloc && newAbsAlloc.MarketStatus is not MarketStatus.Trading)
        continue;

      // Determine relative allocation.
      var relAlloc = totalAbsAlloc == 0 || newAbsAlloc == null ? 0 : newAbsAlloc.AbsAlloc / totalAbsAlloc;

      // Determine new quote amount.
      var newAmountQuote = relAlloc * curBalance.AmountQuoteTotal;

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
        continue;

      // Determine relative allocation.
      var relAlloc = totalAbsAlloc == 0 ? 0 : newAbsAlloc.AbsAlloc / totalAbsAlloc;

      // Determine new quote amount.
      var newAmountQuote = relAlloc * curBalance.AmountQuoteTotal;

      yield return new AllocDiffReqDto(
        newAbsAlloc.Market,
        0,
        0,
        -newAmountQuote);
    }
  }

  /// <summary>
  /// Places <paramref name="orderReq"/> and, on success, verifies it has ended.
  /// For a <see cref="OrderType.Limit"/> request, delegates to <see cref="PlaceLimitThenFallback"/>,
  /// which may place a second market order for any unfilled remainder — so this can return more
  /// than one <see cref="OrderDto"/>. For anything else, always returns exactly one.
  /// </summary>
  /// <param name="exchange"></param>
  /// <param name="credentials"></param>
  /// <param name="orderReq"></param>
  /// <param name="source"></param>
  /// <param name="cancel"><inheritdoc cref="VerifyOrderEnded" path="/param[@name='cancel']"/></param>
  /// <returns>The placed (and, where possible, ended) order(s).</returns>
  private async Task<OrderDto[]> PlaceAndVerifyOrder(
    IExchange exchange, ExchangeCredentials credentials, OrderReqDto orderReq, string source, bool cancel)
  {
    if (orderReq.Type != OrderType.Limit)
      return [await PlaceAndVerifySingleOrder(exchange, credentials, orderReq, source, cancel)];

    return await PlaceLimitThenFallback(exchange, credentials, orderReq, source);
  }

  /// <summary>
  /// Places a <see cref="OrderType.Limit"/> order at the current best bid (sell) / best ask (buy),
  /// waits up to <see cref="VerifyOrderEnded"/>'s default budget for it to fill, and — if it
  /// didn't fully fill in time — cancels it and places a plain <see cref="OrderType.Market"/>
  /// order for exactly the remaining amount, so the leg always completes.
  /// </summary>
  /// <returns>
  /// A single-element array if the limit order filled outright, or a two-element array
  /// [limit leg (<see cref="OrderDto.IsSuperseded"/> = true), market fallback leg] otherwise.
  /// </returns>
  private async Task<OrderDto[]> PlaceLimitThenFallback(
    IExchange exchange, ExchangeCredentials credentials, OrderReqDto orderReq, string source)
  {
    decimal limitPrice;

    try
    {
      var bidAsk = await exchange.GetBestBidAsk(credentials, orderReq.Market);

      limitPrice = orderReq.Side == OrderSide.Sell ? bidAsk?.Bid ?? 0 : bidAsk?.Ask ?? 0;

      if (limitPrice <= 0)
        throw new InvalidOperationException($"No best bid/ask available for market {orderReq.Market}.");
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex,
        "Failed to determine a limit price for market {Market} on exchange {Exchange} — falling back to a market order directly.",
        orderReq.Market, exchange.GetType().Name);

      var marketOrder = await PlaceAndVerifySingleOrder(
        exchange, credentials, ToMarketOrder(orderReq), source, cancel: true);

      return [marketOrder];
    }

    var limitAmount = orderReq.Amount is decimal amount
      ? TruncateToDecimals(amount, 8)
      : orderReq.AmountQuote is decimal amountQuote
        ? TruncateToDecimals(amountQuote / limitPrice, 8)
        : 0;

    if (limitAmount <= 0)
      return [];

    var limitReq = new OrderReqDto()
    {
      Market = orderReq.Market,
      Side = orderReq.Side,
      Type = OrderType.Limit,
      Price = limitPrice,
      Amount = limitAmount,
    };

    // Always cancel a limit order that hasn't (fully) filled by the timeout — the whole point of
    // this path is to fall back to a market order for the remainder, so a resting limit order can
    // never be allowed to just sit there past this point.
    var limitOrder = await PlaceAndVerifySingleOrder(exchange, credentials, limitReq, source, cancel: true);

    if (limitOrder.Status == OrderStatus.Filled || limitOrder.AmountRemaining <= 0)
      return [limitOrder];

    limitOrder.IsSuperseded = true;

    // Value the remainder at the limit price used to size the order — good enough to pick a
    // branch and, in the dust branch, to report on; the market fallback itself always settles at
    // whatever the current price actually is.
    var remainingAmountQuote = limitOrder.AmountRemaining * limitPrice;

    OrderReqDto fallbackReq;

    // Mirror the dust-prevention branch in SellOveragesAndVerify: a remainder below the exchange's
    // minimum order size would get rejected as an AmountQuote-based order, but exchanges commonly
    // still allow a full-position liquidation by exact (asset-decimals-rounded) Amount. This only
    // applies to sells — a dust buy remainder has no such escape hatch (there's nothing existing
    // to fully acquire), so it's simply dropped.
    if (remainingAmountQuote < exchange.MinOrderSizeInQuote)
    {
      if (orderReq.Side != OrderSide.Sell)
        return [limitOrder];

      // Honor decimals precision for the amount of this asset.
      var assetData = await exchange.GetAsset(credentials, orderReq.Market.BaseSymbol);
      var decimals = assetData?.Decimals;

      var dustAmount = decimals is not int
        ? limitOrder.AmountRemaining
        : TruncateToDecimals(limitOrder.AmountRemaining, (int)decimals);

      if (dustAmount <= 0)
        return [limitOrder];

      fallbackReq = new OrderReqDto()
      {
        Market = orderReq.Market,
        Side = OrderSide.Sell,
        Type = OrderType.Market,
        Amount = dustAmount,
      };
    }
    else
    {
      fallbackReq = new OrderReqDto()
      {
        Market = orderReq.Market,
        Side = orderReq.Side,
        Type = OrderType.Market,
        AmountQuote = remainingAmountQuote,
      };
    }

    var fallbackOrder = await PlaceAndVerifySingleOrder(exchange, credentials, fallbackReq, source, cancel: true);

    return [limitOrder, fallbackOrder];
  }

  /// <summary>
  /// Copies <paramref name="orderReq"/> into a plain <see cref="OrderType.Market"/> request,
  /// keeping whichever of <see cref="OrderReqDto.Amount"/>/<see cref="OrderReqDto.AmountQuote"/>
  /// was already specified (both are valid for market orders).
  /// </summary>
  private static OrderReqDto ToMarketOrder(OrderReqDto orderReq)
  {
    return new()
    {
      Market = orderReq.Market,
      Side = orderReq.Side,
      Type = OrderType.Market,
      Amount = orderReq.Amount,
      AmountQuote = orderReq.AmountQuote,
    };
  }

  /// <summary>
  /// Truncates (rounds toward zero) <paramref name="amount"/> to <paramref name="decimals"/>
  /// decimal places, so a computed order amount never rounds up past what's actually available.
  /// </summary>
  private static decimal TruncateToDecimals(decimal amount, int decimals)
  {
    var factor = (decimal)Math.Pow(10, decimals);

    return Math.Floor(amount * factor) / factor;
  }

  /// <summary>
  /// Places <paramref name="orderReq"/> and, on success, verifies it has ended.
  /// Never throws: a <see cref="Results.Result{TSuccess, TErrCode}"/> failure carrying no order
  /// payload, or any unexpected exception raised while placing or verifying the order, is
  /// converted into a synthetic <see cref="OrderStatus.Failed"/> <see cref="OrderDto"/> instead
  /// of propagating. This guarantees every order in a <see cref="Task.WhenAll{TResult}"/> batch
  /// resolves to a result, so one failing order can never discard the results of the others.
  /// </summary>
  /// <param name="exchange"></param>
  /// <param name="credentials"></param>
  /// <param name="orderReq"></param>
  /// <param name="source"></param>
  /// <param name="cancel"><inheritdoc cref="VerifyOrderEnded" path="/param[@name='cancel']"/></param>
  /// <returns>The placed (and, where possible, ended) order, or a synthetic failed order.</returns>
  private async Task<OrderDto> PlaceAndVerifySingleOrder(
    IExchange exchange, ExchangeCredentials credentials, OrderReqDto orderReq, string source, bool cancel)
  {
    OrderDto order;

    try
    {
      var result = await exchange.NewOrder(credentials, orderReq, source);

      if (result.Value is null)
      {
        _logger.LogError(
          "Failed to place order for market {Market} on exchange {Exchange}: exchange returned no order payload (error code {ErrorCode}).",
          orderReq.Market, exchange.GetType().Name, result.ErrorCode);

        return NewFailedOrder(orderReq);
      }

      order = result.Value;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to place order for market {Market} on exchange {Exchange}.", orderReq.Market, exchange.GetType().Name);

      return NewFailedOrder(orderReq);
    }

    try
    {
      return await VerifyOrderEnded(exchange, credentials, order, cancel);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to verify order {OrderId} for market {Market} on exchange {Exchange} has ended.", order.Id, order.Market, exchange.GetType().Name);

      // Return the last known state rather than the placement failure,
      // since the order itself was successfully placed.
      return order;
    }
  }

  private static OrderDto NewFailedOrder(OrderReqDto orderReq)
  {
    return new()
    {
      Market = orderReq.Market,
      Side = orderReq.Side,
      Type = orderReq.Type,
      Price = orderReq.Price,
      Amount = orderReq.Amount,
      AmountQuote = orderReq.AmountQuote,
      Status = OrderStatus.Failed,
    };
  }

  public async Task<OrderDto> VerifyOrderEnded(IExchange exchange, ExchangeCredentials credentials, OrderDto order, bool cancel = true, int checks = 60)
  {
    if (exchange is IExchangeOrderNotifications pushExchange && order.Id != null && !order.HasEnded)
    {
      // Wait for a pushed status update instead of polling GetOrder every second. On timeout or
      // any push failure, fall back to a single authoritative REST check rather than re-polling
      // for another full budget of `checks` seconds.
      order = await pushExchange.WaitForOrderEndedAsync(credentials, order, TimeSpan.FromSeconds(checks)) is { } pushed
        ? pushed
        : await exchange.GetOrder(credentials, order.Id, order.Market) ?? order;

      // Reuse `checks == 0` below as the "still open" signal, matching the polling path's semantics.
      checks = order.HasEnded ? 1 : 0;
    }
    else
    {
      while (
        checks > 0 &&
        order.Id != null &&
        !order.HasEnded)
      {
        await Task.Delay(1000);

        order = await exchange.GetOrder(credentials, order.Id, order.Market) ?? order;

        checks--;
      }
    }

    if (cancel && checks == 0)
      try
      {
        order = await exchange.CancelOrder(credentials, order.Id!, order.Market) ?? order;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to cancel order {OrderId} for market {Market} on exchange {Exchange}.", order.Id, order.Market, exchange.GetType().Name);
      }

    return order;
  }

  /// <summary>
  /// Sell pieces of oversized <see cref="Allocation"/>s in order for those to meet <paramref name="newAbsAllocs"/>.
  /// Completes when verified that all triggered sell orders are ended.
  /// </summary>
  /// <param name="exchange"></param>
  /// <param name="credentials"></param>
  /// <param name="newAbsAllocs"></param>
  /// <param name="source"></param>
  /// <param name="config"></param>
  /// <param name="curBalance"></param>
  /// <returns></returns>
  private async Task<OrderDto[]> SellOveragesAndVerify(
    IExchange exchange, ExchangeCredentials credentials, IEnumerable<AbsAllocReqDto> newAbsAllocs, string source, ConfigReqDto config, Balance? curBalance)
  {
    if (null == curBalance)
    {
      var curBalanceResult = await exchange.GetBalance(credentials);
      curBalance = curBalanceResult.Value!;
    }

    var orders =
      GetAllocationQuoteDiffs(exchange, newAbsAllocs, config, curBalance)

      // We can't trade quote currency for quote currency.
      .Where(allocDiff => !allocDiff.Market.BaseSymbol.Equals(exchange.QuoteSymbol))

      // Positive quote differences refer to oversized allocations.
      .Where(allocDiff => allocDiff.AmountQuoteDiff > 0)

      // Construct sell order.
      .Select(allocDiff =>
      {
        var order = new OrderReqDto()
        {
          Market = allocDiff.Market,
          Side = OrderSide.Sell,
          Type = config.UseLimitOrders ? OrderType.Limit : OrderType.Market,
        };

        // Prevent dust.
        if (allocDiff.AmountQuote - allocDiff.AmountQuoteDiff < exchange.MinOrderSizeInQuote)
        {
          // Honor decimals precision for the amount of this asset.
          var assetData = exchange.GetAsset(credentials, allocDiff.Market.BaseSymbol).GetAwaiter().GetResult();
          var decimals = assetData?.Decimals;

          order.Amount = decimals is not int ? allocDiff.Amount : Math.Floor(allocDiff.Amount * (decimal)Math.Pow(10, (int)decimals)) / (decimal)Math.Pow(10, (int)decimals);
        }
        else
        {
          order.AmountQuote = allocDiff.AmountQuoteDiff;
        }

        return order;
      });

    return await SellOveragesAndVerify(exchange, credentials, orders, source);
  }

  /// <summary>
  /// Sell pieces of oversized <see cref="Allocation"/>s as defined in <paramref name="orders"/>.
  /// Completes when verified that all triggered sell orders are ended.
  /// </summary>
  /// <param name="exchange"></param>
  /// <param name="credentials"></param>
  /// <param name="orders"></param>
  /// <param name="source"></param>
  /// <returns></returns>
  private async Task<OrderDto[]> SellOveragesAndVerify(
    IExchange exchange, ExchangeCredentials credentials, IEnumerable<OrderReqDto> orders, string source)
  {
    // The sell task loop ..
    var results = await Task.WhenAll(
      orders

      // Filter for sell orders.
      .Where(order => order.Side == OrderSide.Sell)

      // We can't trade quote currency for quote currency.
      .Where(sellOrder => !sellOrder.Market.BaseSymbol.Equals(exchange.QuoteSymbol))

      // Check if reached minimum order size.
      .Where(sellOrder => sellOrder.AmountQuote >= exchange.MinOrderSizeInQuote || sellOrder.Amount > 0)

      // Round to avoid potentially invalid amount quote.
      .Select(sellOrder =>
      {
        if (sellOrder.AmountQuote is decimal amountQuote)
          sellOrder.AmountQuote = Math.Ceiling(amountQuote * 100) / 100;

        return sellOrder;
      })

      // Sell, then verify the sell order ended. A limit order that falls back to market
      // yields two entries; everything else yields exactly one.
      .Select(sellOrder => PlaceAndVerifyOrder(exchange, credentials, sellOrder, source, cancel: true)));

    return results.SelectMany(orderResults => orderResults).ToArray();
  }

  /// <summary>
  /// Buy to increase undersized <see cref="Allocation"/>s in order for those to meet <paramref name="newAbsAllocs"/>.
  /// Completes when all triggered buy orders are posted.
  /// </summary>
  /// <param name="exchange"></param>
  /// <param name="credentials"></param>
  /// <param name="newAbsAllocs"></param>
  /// <param name="source"></param>
  /// <param name="config"></param>
  /// <param name="curBalance"></param>
  /// <returns></returns>
  private async Task<OrderDto[]> BuyUnderagesAndVerify(
    IExchange exchange, ExchangeCredentials credentials, IEnumerable<AbsAllocReqDto> newAbsAllocs, string source, ConfigReqDto config, Balance? curBalance)
  {
    if (null == curBalance)
    {
      var curBalanceResult = await exchange.GetBalance(credentials);
      curBalance = curBalanceResult.Value!;
    }

    var orders =
      GetAllocationQuoteDiffs(exchange, newAbsAllocs, config, curBalance)

      // We can't trade quote currency for quote currency.
      .Where(allocDiff => !allocDiff.Market.BaseSymbol.Equals(exchange.QuoteSymbol))

      // Negative quote differences refer to undersized allocations.
      .Where(allocDiff => allocDiff.AmountQuoteDiff < 0)

      // Construct buy order.
      .Select(allocDiff => new OrderReqDto()
      {
        Market = allocDiff.Market,
        Side = OrderSide.Buy,
        Type = config.UseLimitOrders ? OrderType.Limit : OrderType.Market,
        AmountQuote = Math.Abs(allocDiff.AmountQuoteDiff),
      });

    return await BuyUnderagesAndVerify(exchange, credentials, orders, source, curBalance);
  }

  /// <summary>
  /// Sell pieces of oversized <see cref="Allocation"/>s as defined in <paramref name="orders"/>.
  /// Completes when verified that all triggered sell orders are ended.
  /// </summary>
  /// <param name="exchange"></param>
  /// <param name="credentials"></param>
  /// <param name="orders"></param>
  /// <param name="source"></param>
  /// <param name="curBalance"></param>
  /// <returns></returns>
  private async Task<OrderDto[]> BuyUnderagesAndVerify(
    IExchange exchange, ExchangeCredentials credentials, IEnumerable<OrderReqDto> orders, string source, Balance? curBalance = null)
  {
    if (null == curBalance)
    {
      var curBalanceResult = await exchange.GetBalance(credentials);
      curBalance = curBalanceResult.Value!;
    }

    List<OrderReqDto> buyOrders = new();

    // Absolute sum of all negative quote differences,
    // using a single multi-purpose enumeration to eliminate redundant enumerations.
    var totalBuy =
      orders

      // Filter for buy orders.
      .Where(order => order.Side == OrderSide.Buy)

      // We can't trade quote currency for quote currency.
      .Where(buyOrder => !buyOrder.Market.BaseSymbol.Equals(exchange.QuoteSymbol))

      // Check if reached minimum order size.
      .Where(buyOrder => buyOrder.AmountQuote >= exchange.MinOrderSizeInQuote)

      // Sum of all negative quote differences.
      .Sum(buyOrder =>
      {
        // Add to list.
        buyOrders.Add(buyOrder);

        return (decimal)buyOrder.AmountQuote!;
      });

    // Multiplication ratio to avoid potentially oversized buy order sizes.
    var ratio = totalBuy == 0 ? 0 :
      Math.Min(totalBuy, curBalance.AmountQuoteAvailable) / totalBuy;

    // The buy task loop, diffs are already filtered ..
    var results = await Task.WhenAll(
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
      .Where(buyOrder => buyOrder.AmountQuote >= exchange.MinOrderSizeInQuote)

      // Buy, then verify the buy order ended. A limit order that falls back to market
      // yields two entries; everything else yields exactly one.
      .Select(buyOrder => PlaceAndVerifyOrder(exchange, credentials, buyOrder, source, cancel: false)));

    return results.SelectMany(orderResults => orderResults).ToArray();
  }

  public async Task<OrderDto[]> Rebalance(
    IExchange exchange,
    ExchangeCredentials credentials,
    ConfigReqDto config,
    IEnumerable<AbsAllocReqDto> newAbsAllocs,
    Balance? curBalance = null,
    string source = "API")
  {
    await using var orderNotificationSession = exchange is IExchangeOrderNotifications pushExchange
      ? await pushExchange.BeginOrderNotificationSessionAsync(credentials)
      : NoOpAsyncDisposable.Instance;

    // Clear the path ..
    _ = await exchange.CancelAllOpenOrders(credentials);

    // Make sure all market statuses of eligible assets are known.
    var absAllocList = await GetTopRankingAllocs(exchange, credentials, newAbsAllocs, config.TopRankingCount);

    // Sell pieces of oversized allocations first,
    // so we have sufficient quote currency available to buy with.
    var sellResults = await SellOveragesAndVerify(exchange, credentials, absAllocList, source, config, curBalance);

    // Then buy to increase undersized allocations.
    var buyResults = await BuyUnderagesAndVerify(exchange, credentials, absAllocList, source, config, curBalance: null);

    // Combined results.
    var orderResults = new OrderDto[sellResults.Length + buyResults.Length];

    Array.Copy(sellResults, 0, orderResults, 0, sellResults.Length);
    Array.Copy(buyResults, 0, orderResults, sellResults.Length, buyResults.Length);

    return orderResults;
  }

  public async Task<OrderDto[]> Rebalance(
    IExchange exchange,
    ExchangeCredentials credentials,
    IEnumerable<OrderReqDto> orders,
    string source = "API")
  {
    await using var orderNotificationSession = exchange is IExchangeOrderNotifications pushExchange
      ? await pushExchange.BeginOrderNotificationSessionAsync(credentials)
      : NoOpAsyncDisposable.Instance;

    // Clear the path ..
    _ = await exchange.CancelAllOpenOrders(credentials);

    // Sell pieces of oversized allocations first,
    // so we have sufficient quote currency available to buy with.
    var sellResults = await SellOveragesAndVerify(exchange, credentials, orders, source);

    // Then buy to increase undersized allocations.
    var buyResults = await BuyUnderagesAndVerify(exchange, credentials, orders, source);

    // Combined results.
    var orderResults = new OrderDto[sellResults.Length + buyResults.Length];

    Array.Copy(sellResults, 0, orderResults, 0, sellResults.Length);
    Array.Copy(buyResults, 0, orderResults, sellResults.Length, buyResults.Length);

    return orderResults;
  }
}
