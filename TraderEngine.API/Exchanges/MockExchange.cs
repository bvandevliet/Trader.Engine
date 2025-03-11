using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Models;
using TraderEngine.Common.Results;

namespace TraderEngine.API.Exchanges;

/// <inheritdoc cref="IExchange"/>
public class MockExchange : IExchange
{
  protected Balance _curBalance;

  public virtual ILogger<IExchange>? Logger { get; } = null;

  public string QuoteSymbol { get; }

  public decimal MinOrderSizeInQuote { get; }

  public decimal MakerFee { get; }

  public decimal TakerFee { get; }

  public string ApiKey { get; set; } = string.Empty;

  public string ApiSecret { get; set; } = string.Empty;

  /// <summary>
  /// <inheritdoc cref="IExchange"/>
  /// </summary>
  /// <param name="quoteSymbol"><inheritdoc cref="QuoteSymbol"/></param>
  /// <param name="minOrderSize"><inheritdoc cref="MinOrderSizeInQuote"/></param>
  /// <param name="makerFee"><inheritdoc cref="MakerFee"/></param>
  /// <param name="takerFee"><inheritdoc cref="TakerFee"/></param>
  /// <param name="curBalance"><inheritdoc cref="Balance"/></param>
  public MockExchange(
    string quoteSymbol,
    decimal minOrderSize,
    decimal makerFee,
    decimal takerFee,
    Balance curBalance)
  {
    QuoteSymbol = quoteSymbol;
    MinOrderSizeInQuote = minOrderSize;
    MakerFee = makerFee;
    TakerFee = takerFee;
    _curBalance = curBalance;

    // Add quote allocation if not present.
    _ = _curBalance.TryAddAllocation(new(QuoteSymbol, QuoteSymbol, 1));
  }

  /// <summary>
  /// Null, if no initial <see cref="Balance"/> was given.
  /// </summary>
  /// <returns></returns>
  public Task<Result<Balance, ExchangeErrCodeEnum>> GetBalance()
  {
    return Task.FromResult(Result<Balance, ExchangeErrCodeEnum>.Success(_curBalance));
  }

  public Task<Result<decimal, ExchangeErrCodeEnum>> TotalDeposited()
  {
    throw new NotImplementedException();
  }

  public Task<Result<decimal, ExchangeErrCodeEnum>> TotalWithdrawn()
  {
    throw new NotImplementedException();
  }

  public Task<MarketDataDto?> GetMarket(MarketReqDto market)
  {
    return Task.FromResult(new MarketDataDto()
    {
      Status = MarketStatus.Trading,
      PricePrecision = 2,
      MinOrderSizeInQuote = MinOrderSizeInQuote,
    })!;
  }

  public Task<AssetDataDto?> GetAsset(string baseSymbol)
  {
    return Task.FromResult(new AssetDataDto()
    {
      BaseSymbol = baseSymbol,
      Name = baseSymbol,
      Decimals = 8,
    })!;
  }

  public Task<decimal> GetPrice(MarketReqDto market)
  {
    throw new NotImplementedException();
  }

  public Task<Result<OrderDto, ExchangeErrCodeEnum>> NewOrder(OrderReqDto order)
  {
    var quoteAlloc = _curBalance.GetAllocation(QuoteSymbol)!;

    var curAlloc = _curBalance.GetAllocation(order.Market.BaseSymbol);

    var newAlloc = curAlloc ?? new(order.Market);

    var returnOrder = new OrderDto()
    {
      Market = order.Market,
      Side = order.Side,
      Status = OrderStatus.Filled,
      Price = order.Price,
      Amount = order.Amount,
      AmountQuote = order.AmountQuote,
    };

    decimal price = curAlloc?.Price ?? 0;

    decimal amountQuote;

    if (order.Side == OrderSide.Buy)
    {
      amountQuote = order.AmountQuote ?? (decimal)(order.Amount! * price);

      // TODO: Fee multiplier causes weird artifacts in expected unit test results !!
      newAlloc.AmountQuote += amountQuote * (1 - TakerFee);

      quoteAlloc.AmountQuote -= amountQuote;

      returnOrder.AmountQuote = amountQuote;
    }
    else
    {
      amountQuote = order.AmountQuote ?? (decimal)(order.Amount! * price);

      newAlloc.AmountQuote -= amountQuote;

      // TODO: Fee multiplier causes weird artifacts in expected unit test results !!
      quoteAlloc.AmountQuote += amountQuote * (1 - TakerFee);

      returnOrder.Amount = order.Amount;
    }

    returnOrder.AmountFilled = price == 0 ? 0 : amountQuote / price;
    returnOrder.AmountQuoteFilled = amountQuote;
    returnOrder.FeePaid = amountQuote * TakerFee;

    if (null == curAlloc)
    {
      _ = (_curBalance?.TryAddAllocation(newAlloc));
    }

    return Task.FromResult(Result<OrderDto, ExchangeErrCodeEnum>.Success(returnOrder));
  }

  public Task<OrderDto?> GetOrder(string orderId, MarketReqDto market)
  {
    throw new NotImplementedException();
  }

  public Task<OrderDto?> CancelOrder(string orderId, MarketReqDto market)
  {
    throw new NotImplementedException();
  }

  public Task<IEnumerable<OrderDto>?> GetOpenOrders(MarketReqDto? market = null)
  {
    throw new NotImplementedException();
  }

  public Task<IEnumerable<OrderDto>?> CancelAllOpenOrders(MarketReqDto? market = null)
  {
    return Task.FromResult(new List<OrderDto>().AsEnumerable())!;
  }

  public Task<Result<IEnumerable<OrderDto>?, ExchangeErrCodeEnum>> SellAllPositions(string? asset = null)
  {
    throw new NotImplementedException();
  }
}

/// <inheritdoc cref="MockExchange"/>
public class SimExchange : MockExchange, IExchange
{
  protected readonly IExchange _instance;

  public override ILogger<IExchange>? Logger => _instance.Logger;

  /// <summary>
  /// <inheritdoc cref="IExchange"/>
  /// </summary>
  public SimExchange(IExchange exchangeService, Balance curBalance)
    : base(
      exchangeService.QuoteSymbol,
      exchangeService.MinOrderSizeInQuote,
      exchangeService.MakerFee,
      exchangeService.TakerFee,
      curBalance)
  {
    _instance = exchangeService;
  }

  public async Task ProcessOrders(IEnumerable<OrderDto> orders)
  {
    foreach (var order in orders)
    {
      // If the order is a buy, we need to add the fee to the amount, since fee is handled by the NewOrder method.
      if (order.Side == OrderSide.Buy)
      {
        order.Amount = order.AmountFilled > 0 ? order.AmountFilled * (1 / (1 - TakerFee)) : order.Amount;
        order.AmountQuote = order.AmountQuoteFilled > 0 ? order.AmountQuoteFilled * (1 / (1 - TakerFee)) : order.AmountQuote;
      }
      // For sell orders, we don't need to add the fee, since it's already subtracted from the amount.
      else
      {
        order.Amount = order.AmountFilled > 0 ? order.AmountFilled : order.Amount;
        order.AmountQuote = order.AmountQuoteFilled > 0 ? order.AmountQuoteFilled : order.AmountQuote;
      }

      _ = await NewOrder(order);
    }
  }

  public new Task<MarketDataDto?> GetMarket(MarketReqDto market) => _instance.GetMarket(market);

  public new Task<AssetDataDto?> GetAsset(string baseSymbol) => _instance.GetAsset(baseSymbol);

  public new Task<decimal> GetPrice(MarketReqDto market) => _instance.GetPrice(market);
}