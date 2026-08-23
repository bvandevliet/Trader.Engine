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

  /// <summary>
  /// Default <see cref="VerifyOrderEnded"/> wait budget (in seconds) used by every internal call
  /// site in this service, most notably <see cref="PlaceLimitThenFallback"/>'s fill-then-cancel
  /// window for a <see cref="OrderType.Limit"/> order. Configurable via the caller's composition
  /// root (see TraderEngine.API's "Rebalancing:FillWaitTimeoutSeconds" setting) rather than baked
  /// in here, since this project has no notion of app configuration of its own.
  /// </summary>
  private readonly int _defaultVerifyChecks;

  public RebalancingService(ILogger<RebalancingService> logger, int fillWaitTimeoutSeconds = 60)
  {
    _logger = logger;
    _defaultVerifyChecks = fillWaitTimeoutSeconds;
  }

  private class AllocDriftReqDto : AllocationDto
  {
    public decimal AmountQuoteDrift { get; set; }

    public AllocDriftReqDto(
      MarketReqDto market,
      decimal price,
      decimal amount,
      decimal amountQuoteDrift)
    {
      Market = market;
      Price = price;
      Amount = amount;
      AmountQuote = price * amount;
      AmountQuoteDrift = amountQuoteDrift;
    }
  }

  public async Task<TargetAllocReqDto> FetchMarketStatus(IExchange exchange, ExchangeCredentials credentials, TargetAllocReqDto targetAlloc)
  {
    // Get market data for the asset and update market status.
    if (targetAlloc.MarketStatus == MarketStatus.Unknown)
    {
      var marketDto = new MarketReqDto(exchange.QuoteSymbol, targetAlloc.Market.BaseSymbol);

      var marketData = await exchange.GetMarket(credentials, marketDto);

      targetAlloc.MarketStatus = marketData?.Status ?? MarketStatus.Unknown;
    }

    return targetAlloc;
  }

  public async Task<List<TargetAllocReqDto>> GetTopRankingAllocs(IExchange exchange, ExchangeCredentials credentials, IEnumerable<TargetAllocReqDto> targetAllocs, int topRankingCount)
  {
    var targetAllocsList = new List<TargetAllocReqDto>();

    foreach (var targetAlloc in targetAllocs)
    {
      var targetAllocUpdated = await FetchMarketStatus(exchange, credentials, targetAlloc);

      if (targetAlloc.MarketStatus != MarketStatus.Unknown)
      {
        // Expecting the collection to be already ordered by market cap.
        topRankingCount--;
        targetAllocsList.Add(targetAllocUpdated);
      }

      if (topRankingCount <= 0)
        break;
    }

    return targetAllocsList;
  }

  /// <summary>
  /// Get current drift in quote currency when comparing target allocations in
  /// <paramref name="targetAllocs"/> against current allocations in <paramref name="curBalance"/>.
  /// </summary>
  /// <param name="exchange"></param>
  /// <param name="targetAllocs"></param>
  /// <param name="config"></param>
  /// <param name="curBalance"></param>
  /// <returns>Collection of current <see cref="Allocation"/>s and their drift in quote currency.</returns>
  private static IEnumerable<AllocDriftReqDto> GetAllocationQuoteDrifts(
    IExchange exchange, IEnumerable<TargetAllocReqDto> targetAllocs, ConfigReqDto config, Balance curBalance)
  {
    // Eligible targets: tradable, or already held (so an untradeable position we currently hold
    // is still recognized below and left alone, rather than treated as "not targeted at all" and
    // fully sold off) — denominated in this exchange's quote currency. Keyed by market instead of
    // linearly scanned per current allocation below.
    var targetAllocsByMarket = targetAllocs
      .Where(targetAlloc => targetAlloc.MarketStatus is MarketStatus.Trading
        || null != curBalance.GetAllocation(targetAlloc.Market.BaseSymbol))
      .Where(targetAlloc => targetAlloc.Market.QuoteSymbol.Equals(exchange.QuoteSymbol))
      .ToDictionary(targetAlloc => targetAlloc.Market);

    var totalTargetWeight = targetAllocsByMarket.Values.Sum(targetAlloc => targetAlloc.TargetWeight);

    // Relative quote allocation (including takeout).
    var quoteRelAlloc = curBalance.AmountQuoteTotal == 0 ? 0 : Math.Max(0, Math.Min(1,
      config.QuoteTakeout / curBalance.AmountQuoteTotal + config.QuoteAllocation / 100));

    // Scale total sum of absolute allocation values to account for relative quote allocation.
    // NOTE: No need to add quote allocation, since it's already been accounted for in the total abs value.
    var div = 1 - quoteRelAlloc;
    totalTargetWeight = div == 0 ? 0 : totalTargetWeight / div;

    decimal newAmountQuote(TargetAllocReqDto? targetAlloc)
    {
      return (totalTargetWeight == 0 || targetAlloc == null ? 0 : targetAlloc.TargetWeight / totalTargetWeight) * curBalance.AmountQuoteTotal;
    }

    // Every market either currently held or targeted — the full set this diff needs to cover, in
    // one pass instead of "current allocations, then whichever targets weren't already matched".
    var markets = curBalance.Allocations.Select(alloc => alloc.Market).Union(targetAllocsByMarket.Keys);

    foreach (var market in markets)
    {
      var curAlloc = curBalance.GetAllocation(market.BaseSymbol);
      targetAllocsByMarket.TryGetValue(market, out var targetAlloc);

      if (curAlloc != null)
      {
        // Currently held, with a known but untradeable target: leave it alone.
        if (targetAlloc != null && targetAlloc.MarketStatus is not MarketStatus.Trading)
          continue;

        yield return new AllocDriftReqDto(curAlloc.Market, curAlloc.Price, curAlloc.Amount, curAlloc.AmountQuote - newAmountQuote(targetAlloc));
      }
      else
      {
        // Never held, and its only target is untradeable: nothing to buy into.
        if (targetAlloc!.MarketStatus is not MarketStatus.Trading)
          continue;

        yield return new AllocDriftReqDto(targetAlloc.Market, 0, 0, -newAmountQuote(targetAlloc));
      }
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
  /// <param name="claimTopUp">
  /// <inheritdoc cref="PlaceLimitThenFallback" path="/param[@name='claimTopUp']"/> Ignored outside
  /// the limit-then-fallback path (a plain market order never leaves a dust remainder to top up).
  /// </param>
  /// <returns>
  /// The placed (and, where possible, ended) order(s), plus whatever extra was claimed via
  /// <paramref name="claimTopUp"/> to round a buy-side dust remainder up to the exchange minimum
  /// (always 0 unless that happened — see <see cref="PlaceLimitThenFallback"/>). The caller is
  /// responsible for folding this back into its own claim/settle accounting — see
  /// <see cref="ClaimAndPlaceBuy"/> for the only caller that passes a non-null <paramref name="claimTopUp"/>.
  /// </returns>
  private async Task<(OrderDto[] Orders, decimal ToppedUpFullCost)> PlaceAndVerifyOrder(
    IExchange exchange, ExchangeCredentials credentials, OrderReqDto orderReq, string source, bool cancel,
    Func<decimal, decimal, Task<decimal>>? claimTopUp = null)
  {
    if (orderReq.Type != OrderType.Limit)
    {
      // Unlike a limit order (whose exact base-asset Amount is always known before placing, see
      // PlaceLimitThenFallback), a market order sized by AmountQuote only has its base-asset
      // quantity estimated here via the current best bid/ask — the exchange settles at whatever
      // the actual fill price turns out to be, so this is a best-effort proactive check, not a
      // guarantee; the exchange itself remains the final arbiter either way.
      var marketAmount = await ResolveMarketOrderBaseAmount(exchange, credentials, orderReq);

      if (marketAmount is decimal amount
        && !await ClearsBaseAssetMinimum(exchange, credentials, orderReq.Market, orderReq.Side, amount))
        return ([], 0);

      return ([await PlaceAndVerifySingleOrder(exchange, credentials, orderReq, source, cancel)], 0);
    }

    return await PlaceLimitThenFallback(exchange, credentials, orderReq, source, claimTopUp);
  }

  /// <summary>
  /// Estimates the base-asset quantity a market order will actually request: <see cref="OrderReqDto.Amount"/>
  /// directly if already given (e.g. a dust/full-liquidation sell), otherwise <see cref="OrderReqDto.AmountQuote"/>
  /// divided by the current best bid (sell) / ask (buy). Returns <see langword="null"/> if neither
  /// is resolvable (no explicit Amount and no book data available) — the caller should treat that
  /// as "can't check, let the exchange decide" rather than a hard failure.
  /// </summary>
  private static async Task<decimal?> ResolveMarketOrderBaseAmount(
    IExchange exchange, ExchangeCredentials credentials, OrderReqDto orderReq)
  {
    if (orderReq.Amount is decimal amount)
      return amount;

    if (orderReq.AmountQuote is not decimal amountQuote)
      return null;

    var bidAsk = await exchange.GetBestBidAsk(credentials, orderReq.Market);
    var price = orderReq.Side == OrderSide.Sell ? bidAsk?.Bid : bidAsk?.Ask;

    return price is decimal p and > 0 ? amountQuote / p : null;
  }

  /// <summary>
  /// Checks <paramref name="amount"/> (base-asset units) against the market's own per-market
  /// minimum base-asset order size (<see cref="MarketDataDto.MinOrderSizeInBase"/>) — a flat
  /// quote-value check alone (<see cref="IExchange.MinOrderSizeInQuote"/>) isn't sufficient, since
  /// an amount can clear that flat floor yet still fall under this specific market's own,
  /// separately-enforced base-asset floor. Confirmed live on Bitvavo: a 30.811819 ADA sell
  /// (~€5.12, above the €5 quote minimum) was rejected outright (errorCode 212) this way.
  /// </summary>
  private async Task<bool> ClearsBaseAssetMinimum(
    IExchange exchange, ExchangeCredentials credentials, MarketReqDto market, OrderSide side, decimal amount)
  {
    var marketData = await exchange.GetMarket(credentials, market);

    if (marketData?.MinOrderSizeInBase is not decimal minOrderSizeInBase || amount >= minOrderSizeInBase)
      return true;

    _logger.LogInformation(
      "Dropping {Side} order for market {Market}: amount {Amount} is below the exchange's minimum base-asset order size of {MinOrderSizeInBase}.",
      side, market.ToString().SanitizeForLog(), amount, minOrderSizeInBase);

    return false;
  }

  /// <summary>
  /// Places a <see cref="OrderType.Limit"/> order at the current best bid (sell) / best ask (buy),
  /// waits up to <see cref="_defaultVerifyChecks"/> seconds for it to fill, and — if it didn't
  /// fully fill in time — cancels it and places a plain <see cref="OrderType.Market"/> order for
  /// exactly the remaining amount, so the leg always completes.
  /// </summary>
  /// <param name="claimTopUp">
  /// Requests up to <c>requested</c> more (full-cost, fee-inclusive units) from the caller's own
  /// budget, granting it only if at least <c>minClaimable</c> can be given — mirroring
  /// <see cref="BudgetLedger.ClaimAsync"/>'s own contract, but as a narrow capability rather than
  /// exposing the ledger itself here, so this method's only interaction with the caller's budget is
  /// "ask for more, get told how much" — the caller (<see cref="ClaimAndPlaceBuy"/>, the only one
  /// that passes a non-null delegate) remains the sole owner of the actual claim/settle bookkeeping.
  /// Used only for a <see cref="OrderSide.Buy"/> dust remainder that falls under the exchange
  /// minimum, to round the fallback order up to the exchange minimum instead of dropping it. Pass
  /// <see langword="null"/> to always drop such a remainder instead (e.g. for sells, which already
  /// have their own full-liquidation escape hatch and need no extra funds to use it).
  /// </param>
  /// <returns>
  /// <c>Orders</c>: a single-element array if the limit order filled outright, or a two-element
  /// array [limit leg (<see cref="OrderDto.IsSuperseded"/> = true), market fallback leg] otherwise.
  /// <c>ToppedUpFullCost</c>: whatever extra was granted via <paramref name="claimTopUp"/> to fund a
  /// buy-side dust top-up (0 unless that happened) — the caller must fold this into its own claim
  /// total before settling, so the ledger's <c>_inFlight</c> tracking for that extra claim actually
  /// gets released (see <see cref="ClaimAndPlaceBuy"/>'s own remarks).
  /// </returns>
  private async Task<(OrderDto[] Orders, decimal ToppedUpFullCost)> PlaceLimitThenFallback(
    IExchange exchange, ExchangeCredentials credentials, OrderReqDto orderReq, string source,
    Func<decimal, decimal, Task<decimal>>? claimTopUp)
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
        orderReq.Market.ToString().SanitizeForLog(), exchange.GetType().Name);

      var marketOrder = await PlaceAndVerifySingleOrder(
        exchange, credentials, ToMarketOrder(orderReq), source, cancel: true);

      return ([marketOrder], 0);
    }

    // Honor decimals precision for the amount of this asset — a limit order (unlike a market
    // order sized by AmountQuote) always requires an explicit Amount, so an over-precise value
    // here risks outright rejection by the exchange rather than silent server-side rounding.
    var limitAssetData = await exchange.GetAsset(credentials, orderReq.Market.BaseSymbol);
    var limitDecimals = limitAssetData?.Decimals;

    var limitAmount = orderReq.Amount is decimal amount
      ? TruncateToDecimals(amount, limitDecimals ?? 8)
      : orderReq.AmountQuote is decimal amountQuote
        ? TruncateToDecimals(amountQuote / limitPrice, limitDecimals ?? 8)
        : 0;

    if (limitAmount <= 0)
      return ([], 0);

    // Checked here, right after Amount is finalized, since that's the only point this service
    // knows the exact base-asset quantity a limit order will actually request.
    if (!await ClearsBaseAssetMinimum(exchange, credentials, orderReq.Market, orderReq.Side, limitAmount))
      return ([], 0);

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

    // A Failed status here means the limit order was rejected outright at placement (e.g. bad
    // price/amount precision, insufficient balance) rather than resting-then-timing-out — nothing
    // was ever superseded, and a market order for the same amount would fail for the same reason,
    // so there's nothing to gain from attempting it.
    if (limitOrder.Status is OrderStatus.Filled or OrderStatus.Failed || limitOrder.AmountRemaining <= 0)
      return ([limitOrder], 0);

    limitOrder.IsSuperseded = true;

    // Value the remainder at the limit price used to size the order — good enough to pick a
    // branch and, in the dust branch, to report on; the market fallback itself always settles at
    // whatever the current price actually is.
    var remainingAmountQuote = limitOrder.AmountRemaining * limitPrice;

    OrderReqDto fallbackReq;
    var toppedUpFullCost = 0m;

    // Mirror the dust-prevention branch in SellOveragesAndVerify: a remainder below the exchange's
    // minimum order size would get rejected as an AmountQuote-based order, but exchanges commonly
    // still allow a full-position liquidation by exact (asset-decimals-rounded) Amount. Sells use
    // that escape hatch directly (no extra funds needed to sell what's already held); a buy has
    // nothing to fully acquire, so it can only be rescued by topping the remainder up to the
    // exchange minimum with a small extra claim against the shared batch budget, if one was given.
    if (remainingAmountQuote < exchange.MinOrderSizeInQuote)
    {
      if (orderReq.Side != OrderSide.Sell)
      {
        var toppedUpAmountQuote = await TryClaimDustTopUp(exchange, credentials, orderReq.Market, claimTopUp, remainingAmountQuote);

        if (toppedUpAmountQuote is null)
        {
          _logger.LogInformation(
            "Dropping unfilled buy remainder for market {Market}: {RemainingAmountQuote} is below the exchange minimum of {MinOrderSizeInQuote}, no fallback order placed.",
            orderReq.Market.ToString().SanitizeForLog(), remainingAmountQuote, exchange.MinOrderSizeInQuote);

          return ([limitOrder], 0);
        }

        var (toppedUpAmountQuoteValue, fullCost) = toppedUpAmountQuote.Value;

        toppedUpFullCost = fullCost;

        _logger.LogInformation(
          "Rounding up unfilled buy remainder for market {Market}: {RemainingAmountQuote} topped up to the exchange minimum of {MinOrderSizeInQuote} using {ToppedUpFullCost} claimed from the batch budget.",
          orderReq.Market.ToString().SanitizeForLog(), remainingAmountQuote, exchange.MinOrderSizeInQuote, toppedUpFullCost);

        fallbackReq = new OrderReqDto()
        {
          Market = orderReq.Market,
          Side = orderReq.Side,
          Type = OrderType.Market,
          AmountQuote = toppedUpAmountQuoteValue,
        };
      }
      else
      {
        // Honor decimals precision for the amount of this asset.
        var assetData = await exchange.GetAsset(credentials, orderReq.Market.BaseSymbol);
        var decimals = assetData?.Decimals;

        var dustAmount = decimals is not int
          ? limitOrder.AmountRemaining
          : TruncateToDecimals(limitOrder.AmountRemaining, (int)decimals);

        if (dustAmount <= 0)
          return ([limitOrder], 0);

        fallbackReq = new OrderReqDto()
        {
          Market = orderReq.Market,
          Side = OrderSide.Sell,
          Type = OrderType.Market,
          Amount = dustAmount,
        };
      }
    }
    else
    {
      fallbackReq = new OrderReqDto()
      {
        Market = orderReq.Market,
        Side = orderReq.Side,
        Type = OrderType.Market,
        AmountQuote = RoundAmountQuote(remainingAmountQuote, orderReq.Side),
      };
    }

    var fallbackOrder = await PlaceAndVerifySingleOrder(exchange, credentials, fallbackReq, source, cancel: true);

    return ([limitOrder, fallbackOrder], toppedUpFullCost);
  }

  /// <summary>
  /// Attempts to claim (via <paramref name="claimTopUp"/>, in full-cost, fee-inclusive units — see
  /// <see cref="ClaimAndPlaceBuy"/>) just enough to round <paramref name="remainingAmountQuote"/>
  /// up to <see cref="IExchange.MinOrderSizeInQuote"/>, rather than leaving a buy-side dust
  /// remainder unrescuable purely because it happened to land under the exchange floor. All-or-
  /// nothing: a claim that can't fully close the gap wouldn't clear the minimum anyway, so it's
  /// worthless — <paramref name="claimTopUp"/> is called with its <c>minClaimable</c> argument
  /// equal to the full ask, guaranteeing a non-zero result would actually close it.
  /// </summary>
  /// <returns>
  /// <see langword="null"/> if no <paramref name="claimTopUp"/> was given or nothing could be
  /// claimed (the caller should drop the remainder in that case); otherwise the topped-up order's
  /// <c>AmountQuote</c> and the full-cost amount actually claimed for it.
  /// </returns>
  private async Task<(decimal AmountQuote, decimal FullCost)?> TryClaimDustTopUp(
    IExchange exchange, ExchangeCredentials credentials, MarketReqDto market,
    Func<decimal, decimal, Task<decimal>>? claimTopUp, decimal remainingAmountQuote)
  {
    if (claimTopUp is null)
      return null;

    var takerFee = await exchange.GetTakerFee(credentials, market);

    var topUpAmountQuote = exchange.MinOrderSizeInQuote - remainingAmountQuote;
    var topUpFullCost = topUpAmountQuote * (1 + takerFee);

    var claimedFullCost = await claimTopUp(topUpFullCost, topUpFullCost);

    if (claimedFullCost <= 0)
      return null;

    var toppedUpAmountQuote = RoundAmountQuote(remainingAmountQuote + topUpAmountQuote, OrderSide.Buy);

    return (toppedUpAmountQuote, claimedFullCost);
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
  /// Rounds <paramref name="amountQuote"/> to the 2 decimals exchanges commonly require for an
  /// AmountQuote-based order, away from zero: up for a <see cref="OrderSide.Sell"/> so the
  /// remainder is fully liquidated rather than left short of what's actually available to sell,
  /// down for a <see cref="OrderSide.Buy"/> so it never overspends what's actually available to
  /// spend.
  /// </summary>
  private static decimal RoundAmountQuote(decimal amountQuote, OrderSide side)
  {
    return side == OrderSide.Sell
      ? Math.Ceiling(amountQuote * 100) / 100
      : Math.Floor(amountQuote * 100) / 100;
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

      // A failure carries its own synthetic OrderDto (Status = Failed, for callers that want to
      // report on the attempt) rather than a null Value — checking Value alone would treat every
      // exchange-side rejection (bad price/amount precision, insufficient balance, etc.) as if the
      // order had actually been placed, silently dropping the failure instead of surfacing it.
      if (result.ErrorCode != ExchangeErrCodeEnum.Ok || result.Value is null)
      {
        _logger.LogError(
          "Failed to place order for market {Market} on exchange {Exchange}: {ErrorCode} {Summary}",
          orderReq.Market.ToString().SanitizeForLog(), exchange.GetType().Name, result.ErrorCode, result.Summary.SanitizeForLog());

        return result.Value ?? NewFailedOrder(orderReq);
      }

      order = result.Value;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to place order for market {Market} on exchange {Exchange}.", orderReq.Market.ToString().SanitizeForLog(), exchange.GetType().Name);

      return NewFailedOrder(orderReq);
    }

    try
    {
      return await VerifyOrderEnded(exchange, credentials, order, cancel, _defaultVerifyChecks);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to verify order {OrderId} for market {Market} on exchange {Exchange} has ended.", order.Id, order.Market.ToString().SanitizeForLog(), exchange.GetType().Name);

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
        _logger.LogError(ex, "Failed to cancel order {OrderId} for market {Market} on exchange {Exchange}.", order.Id, order.Market.ToString().SanitizeForLog(), exchange.GetType().Name);
      }

    return order;
  }

  /// <summary>
  /// Builds sell order requests for oversized <see cref="Allocation"/>s from already-computed
  /// <paramref name="allocDrifts"/> (positive drift = oversized). Raw output only — still needs to
  /// go through <see cref="PrepareSellOrders"/> before execution.
  /// </summary>
  private static IEnumerable<OrderReqDto> BuildSellOrdersFromDrifts(
    IExchange exchange, ExchangeCredentials credentials, IEnumerable<AllocDriftReqDto> allocDrifts, ConfigReqDto config)
  {
    return allocDrifts
      .Where(allocDrift => allocDrift.AmountQuoteDrift > 0)
      .Select(allocDrift =>
      {
        var order = new OrderReqDto()
        {
          Market = allocDrift.Market,
          Side = OrderSide.Sell,
          Type = config.UseLimitOrders ? OrderType.Limit : OrderType.Market,
        };

        // Prevent dust.
        if (allocDrift.AmountQuote - allocDrift.AmountQuoteDrift < exchange.MinOrderSizeInQuote)
        {
          // Honor decimals precision for the amount of this asset.
          var assetData = exchange.GetAsset(credentials, allocDrift.Market.BaseSymbol).GetAwaiter().GetResult();
          var decimals = assetData?.Decimals;

          order.Amount = decimals is not int ? allocDrift.Amount : TruncateToDecimals(allocDrift.Amount, (int)decimals);
        }
        else
        {
          order.AmountQuote = allocDrift.AmountQuoteDrift;
        }

        return order;
      });
  }

  /// <summary>
  /// Builds buy order requests for undersized <see cref="Allocation"/>s from already-computed
  /// <paramref name="allocDrifts"/> (negative drift = undersized). Raw output only — still needs
  /// to go through <see cref="PrepareBuyOrders"/> before execution.
  /// </summary>
  private static IEnumerable<OrderReqDto> BuildBuyOrdersFromDrifts(
    IEnumerable<AllocDriftReqDto> allocDrifts, ConfigReqDto config)
  {
    return allocDrifts
      .Where(allocDrift => allocDrift.AmountQuoteDrift < 0)
      .Select(allocDrift => new OrderReqDto()
      {
        Market = allocDrift.Market,
        Side = OrderSide.Buy,
        Type = config.UseLimitOrders ? OrderType.Limit : OrderType.Market,
        AmountQuote = Math.Abs(allocDrift.AmountQuoteDrift),
      });
  }

  /// <summary>
  /// Filters and normalizes raw sell order requests for execution: quote-to-quote and sub-minimum
  /// orders are dropped, and any explicit <see cref="OrderReqDto.AmountQuote"/> is rounded.
  /// </summary>
  private static List<OrderReqDto> PrepareSellOrders(IExchange exchange, IEnumerable<OrderReqDto> orders)
  {
    return orders
      .Where(order => order.Side == OrderSide.Sell)
      .Where(sellOrder => !sellOrder.Market.BaseSymbol.Equals(exchange.QuoteSymbol))
      .Where(sellOrder => sellOrder.AmountQuote >= exchange.MinOrderSizeInQuote || sellOrder.Amount > 0)
      .Select(sellOrder =>
      {
        if (sellOrder.AmountQuote is decimal amountQuote)
          sellOrder.AmountQuote = RoundAmountQuote(amountQuote, OrderSide.Sell);

        return sellOrder;
      })
      .ToList();
  }

  /// <summary>
  /// Filters raw buy order requests for execution: quote-to-quote orders and any request already
  /// below the exchange minimum (before ratio scaling) are dropped and logged.
  /// </summary>
  private List<OrderReqDto> PrepareBuyOrders(IExchange exchange, IEnumerable<OrderReqDto> orders)
  {
    return orders
      .Where(order => order.Side == OrderSide.Buy)
      .Where(buyOrder => !buyOrder.Market.BaseSymbol.Equals(exchange.QuoteSymbol))
      .Where(buyOrder =>
      {
        if (buyOrder.AmountQuote >= exchange.MinOrderSizeInQuote)
          return true;

        _logger.LogInformation(
          "Skipping buy order for market {Market}: {AmountQuote} is below the exchange minimum of {MinOrderSizeInQuote}.",
          buyOrder.Market.ToString().SanitizeForLog(), buyOrder.AmountQuote, exchange.MinOrderSizeInQuote);

        return false;
      })
      .ToList();
  }

  /// <summary>
  /// Runs every sell and buy leg of a rebalance run concurrently against a shared <see cref="BudgetLedger"/>,
  /// rather than gating the entire buy phase on every sell finishing first. Each sell leg deposits
  /// its own exchange-settled net proceeds into the ledger as soon as it resolves; each buy leg
  /// requests its own full, unscaled target from the ledger, starting as soon as enough has
  /// actually landed rather than waiting for the slowest sell in the batch. There is no upfront
  /// fairness estimate here: <see cref="BudgetLedger"/> itself only ever scales a claim down once
  /// it knows the account's true final total (see <see cref="BudgetLedger.Complete"/>), so a buy
  /// leg can never be dropped over an inaccurate guess when the funds were genuinely there.
  /// </summary>
  private async Task<OrderDto[]> ExecuteInterleaved(
    IExchange exchange, ExchangeCredentials credentials,
    List<OrderReqDto> sellOrders, List<OrderReqDto> buyOrders, string source, decimal initialAvailableWithFeeBuffer)
  {
    var ledger = new BudgetLedger(initialAvailableWithFeeBuffer);

    var sellTasks = sellOrders
      .Select(async sellOrder =>
      {
        // Sells never top up a dust remainder (no ledger passed) — they already have their own
        // full-liquidation escape hatch and need no extra funds to use it.
        var (results, _) = await PlaceAndVerifyOrder(exchange, credentials, sellOrder, source, cancel: true);

        // Net proceeds actually settled for this leg, exchange-reported on the resolved order(s)
        // — authoritative for this leg specifically, not an estimate. A limit-then-fallback pair
        // contributes both legs' own fills.
        var netProceeds = results.Sum(order => order.AmountQuoteFilled - order.FeePaid);

        ledger.Deposit(netProceeds);

        return results;
      })
      .ToList();

    var sellsCompletion = Task.WhenAll(sellTasks);

    // Every leg requests its own full, unscaled target — see the type-level remarks on
    // BudgetLedger for why there's no upfront ratio/estimate here.
    var buyTasks = buyOrders
      .Select(buyOrder => ClaimAndPlaceBuy(ledger, exchange, credentials, buyOrder, (decimal)buyOrder.AmountQuote!, source))
      .ToList();

    // Every buy leg above has now registered its claim (BudgetLedger.ClaimAsync registers
    // synchronously, before its caller's first real await — see its own remarks), so it's safe to
    // check, once, whether the pool already covers the whole batch together, without any single
    // leg having had a chance to resolve alone first.
    ledger.TryResolvePendingBatch();

    var buysCompletion = Task.WhenAll(buyTasks);

    // Started only now, after buyTasks above has been materialized. This ordering is a genuine
    // guarantee, not an incidental one that merely happens to hold for synchronous test doubles:
    // BudgetLedger.ClaimAsync is itself a plain synchronous method (not `async`), so every claim's
    // registration into the pending queue (or its immediate fast-path grant) completes synchronously
    // the moment ClaimAndPlaceBuy calls it, before that call's first real `await` — meaning by the
    // time `.ToList()` above finishes enumerating buyOrders, every leg is already registered with
    // the ledger, regardless of how slow or fast the underlying exchange actually is. If reconcileTask
    // were started first instead, and it happened to complete before any buy leg had registered
    // (trivially true for a fully synchronous exchange with no sells to wait for), Complete() would
    // fire against an empty pending queue, and every claim would then resolve individually via
    // ClaimAsync's own post-Complete() branch instead of the fair, proportional split Complete()
    // performs across whatever's actually pending, collapsing fairness into first-claimed-wins.
    var reconcileTask = ReconcileLedgerAfterSells(exchange, credentials, sellsCompletion, ledger);

    await Task.WhenAll(sellsCompletion, buysCompletion, reconcileTask);

    return [.. sellsCompletion.Result.SelectMany(r => r), .. buysCompletion.Result.SelectMany(r => r)];
  }

  /// <summary>
  /// Claims <paramref name="tradeValueTarget"/> (in trade-value terms) from <paramref name="ledger"/>
  /// and places (and verifies) a buy order for whatever was actually claimed, if enough cleared the
  /// exchange minimum. Always reports the outcome back via <see cref="BudgetLedger.Settle"/>.
  /// </summary>
  private async Task<OrderDto[]> ClaimAndPlaceBuy(
    BudgetLedger ledger, IExchange exchange, ExchangeCredentials credentials,
    OrderReqDto buyOrder, decimal tradeValueTarget, string source)
  {
    // Bitvavo charges a buy order's fee in quote currency IN ADDITION to its trade value, so the
    // ledger is claimed against (and settled in) full-cost units (trade value + this leg's own
    // fee), not trade value alone — otherwise a leg's own fee would silently draw down whatever's
    // left for legs claiming after it, rather than being reserved out of its own share. Fetched
    // per-market rather than trusting one flat account-wide rate — Bitvavo can place different
    // markets under different fee categories, so the actual rate for this specific market may
    // differ from the account's default.
    var takerFee = await exchange.GetTakerFee(credentials, buyOrder.Market);

    var fullCostRequested = tradeValueTarget * (1 + takerFee);
    var minFullCost = exchange.MinOrderSizeInQuote * (1 + takerFee);

    var claimedFullCost = await ledger.ClaimAsync(fullCostRequested, minFullCost);

    if (claimedFullCost <= 0)
      return [];

    // Rounds toward zero (Math.Floor for a buy), so this can only shrink further, never reclaim
    // back into the fee headroom already reserved above.
    var claimedTradeValue = RoundAmountQuote(claimedFullCost / (1 + takerFee), OrderSide.Buy);

    OrderDto[] results;
    var toppedUpFullCost = 0m;

    if (claimedTradeValue < exchange.MinOrderSizeInQuote)
    {
      _logger.LogInformation(
        "Dropping buy order for market {Market}: could only claim {Claimed} of the exchange minimum {MinOrderSizeInQuote} from the available budget.",
        buyOrder.Market.ToString().SanitizeForLog(), claimedTradeValue, exchange.MinOrderSizeInQuote);

      results = [];
    }
    else
    {
      buyOrder.AmountQuote = claimedTradeValue;

      // PlaceLimitThenFallback never calls ledger.ClaimAsync/Settle itself — it only has the
      // narrow "ask for more, get told how much" capability below, so this remains the ONE place
      // that owns this leg's entire claim/settle lifecycle, top-up included.
      (results, toppedUpFullCost) = await PlaceAndVerifyOrder(
        exchange, credentials, buyOrder, source, cancel: false,
        claimTopUp: ledger.ClaimAsync);
    }

    // Whatever wasn't actually spent (2-decimal rounding remainder, a dropped/failed leg, or a
    // partial fill) goes back to the pool rather than evaporating from the ledger unspent. Folding
    // toppedUpFullCost into the claimed total here (rather than settling it separately) is what
    // makes this a single claim/settle pair for the whole leg: ledger.ClaimAsync was called twice
    // (once here, once inside PlaceLimitThenFallback's dust top-up, if any), each incrementing
    // _inFlight by its own amount — a single Settle call using their SUM is what correctly reverses
    // both increments in one shot. Settling only claimedFullCost here (or subtracting toppedUpFullCost
    // from actuallySpent instead, as an earlier version of this code did) would leave the top-up's
    // own _inFlight contribution permanently stuck, silently under-crediting the pool for whatever
    // else was still pending when BudgetLedger.Complete ran — and would also mishandle a top-up
    // order that failed outright, since actuallySpent (computed from real order fills, not from a
    // subtraction) already reflects that correctly with no extra branching needed here.
    var actuallySpent = results.Sum(order => order.AmountQuoteFilled + order.FeePaid);
    ledger.Settle(claimedFullCost + toppedUpFullCost, actuallySpent);

    return results;
  }

  /// <summary>
  /// Once every sell leg has resolved, reconciles the ledger against one authoritative balance
  /// fetch (see remarks on <see cref="BudgetLedger.Complete"/>), then releases whatever buy legs
  /// are still waiting on the final scraps (or nothing). Best-effort: a failure here doesn't fail
  /// the run — every deposit already fed the ledger directly from each sell's own settled result,
  /// so this fetch is a safety-net correction on top of that, not the only source of truth. Losing
  /// it just means this run misses its one reconciliation pass, not that it hangs or overspends.
  /// </summary>
  private async Task ReconcileLedgerAfterSells(
    IExchange exchange, ExchangeCredentials credentials, Task sellsCompletion, BudgetLedger ledger)
  {
    try
    {
      await sellsCompletion;
    }
    catch
    {
      // PlaceAndVerifyOrder never throws (see its own remarks) — reaching here would mean
      // something unexpected happened in this method's own sell-task wrapper. Still reconcile
      // with whatever landed rather than leaving any waiting buy leg stuck on this ledger forever.
    }

    decimal? actualAvailable = null;

    try
    {
      var balanceResult = await exchange.GetBalance(credentials);

      if (balanceResult.Value is { } balance)
        actualAvailable = balance.AmountQuoteAvailable * (1 - await exchange.GetTakerFee(credentials));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to fetch balance for final buy-budget reconciliation on exchange {Exchange}; proceeding with the locally-tracked total.", exchange.GetType().Name);
    }

    ledger.Complete(actualAvailable);
  }

  /// <summary>
  /// Begins the push-notification session (if the exchange supports one) and clears any open
  /// orders standing in the way, shared setup for both <see cref="Rebalance"/> overloads.
  /// </summary>
  private static async Task<IAsyncDisposable> BeginRebalanceAsync(IExchange exchange, ExchangeCredentials credentials)
  {
    var session = exchange is IExchangeOrderNotifications pushExchange
      ? await pushExchange.BeginOrderNotificationSessionAsync(credentials)
      : NoOpAsyncDisposable.Instance;

    // Clear the path ..
    _ = await exchange.CancelAllOpenOrders(credentials);

    return session;
  }

  public async Task<OrderDto[]> Rebalance(
    IExchange exchange,
    ExchangeCredentials credentials,
    ConfigReqDto config,
    IEnumerable<TargetAllocReqDto> targetAllocs,
    Balance? curBalance = null,
    string source = "API")
  {
    await using var orderNotificationSession = await BeginRebalanceAsync(exchange, credentials);

    // Make sure all market statuses of eligible assets are known.
    var targetAllocList = await GetTopRankingAllocs(exchange, credentials, targetAllocs, config.TopRankingCount);

    if (null == curBalance)
    {
      var curBalanceResult = await exchange.GetBalance(credentials);
      curBalance = curBalanceResult.Value!;
    }

    // Computed once, from the same pre-sell balance, for both legs: an internal sell barely moves
    // AmountQuoteTotal (only the sell's own fee shaves a sliver off it), so target-weight sizing
    // doesn't need to wait for sells to land the way funding (handled by ExecuteInterleaved's
    // BudgetLedger below) genuinely does.
    var allocDrifts = GetAllocationQuoteDrifts(exchange, targetAllocList, config, curBalance).ToList();

    var sellOrders = PrepareSellOrders(exchange, BuildSellOrdersFromDrifts(exchange, credentials, allocDrifts, config));
    var buyOrders = PrepareBuyOrders(exchange, BuildBuyOrdersFromDrifts(allocDrifts, config));

    var initialAvailableWithFeeBuffer = curBalance.AmountQuoteAvailable * (1 - await exchange.GetTakerFee(credentials));

    return await ExecuteInterleaved(exchange, credentials, sellOrders, buyOrders, source, initialAvailableWithFeeBuffer);
  }

  public async Task<OrderDto[]> Rebalance(
    IExchange exchange,
    ExchangeCredentials credentials,
    IEnumerable<OrderReqDto> orders,
    string source = "API")
  {
    await using var orderNotificationSession = await BeginRebalanceAsync(exchange, credentials);

    var orderList = orders.ToList();

    var sellOrders = PrepareSellOrders(exchange, orderList);
    var buyOrders = PrepareBuyOrders(exchange, orderList);

    var curBalanceResult = await exchange.GetBalance(credentials);
    var curBalance = curBalanceResult.Value!;

    var initialAvailableWithFeeBuffer = curBalance.AmountQuoteAvailable * (1 - await exchange.GetTakerFee(credentials));

    return await ExecuteInterleaved(exchange, credentials, sellOrders, buyOrders, source, initialAvailableWithFeeBuffer);
  }
}
