using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Models;

namespace TraderEngine.API.Exchanges;

/// <inheritdoc cref="IExchange"/>
public class MockExchange : IExchange
{
  protected Balance _curBalance;

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
  }

  /// <summary>
  /// Null, if no initial <see cref="Balance"/> was given.
  /// </summary>
  /// <returns></returns>
  public Task<Balance> GetBalance()
  {
    return Task.FromResult(_curBalance);
  }

  public Task<decimal> TotalDeposited()
  {
    throw new NotImplementedException();
  }

  public Task<decimal> TotalWithdrawn()
  {
    throw new NotImplementedException();
  }

  public Task<object?> GetCandles(MarketReqDto market, CandleInterval interval, int limit)
  {
    throw new NotImplementedException();
  }

  public Task<MarketDataDto?> GetMarket(MarketReqDto market)
  {
    throw new NotImplementedException();
  }

  public Task<decimal> GetPrice(MarketReqDto market)
  {
    throw new NotImplementedException();
  }

  public Task<OrderDto> NewOrder(OrderReqDto order)
  {
    Allocation? curAlloc = _curBalance?.GetAllocation(order.Market.BaseSymbol);

    Allocation newAlloc = curAlloc ?? new(order.Market);

    Allocation? quoteAlloc = _curBalance?.GetAllocation(QuoteSymbol);

    Allocation newQuoteAlloc = quoteAlloc ?? new(QuoteSymbol, QuoteSymbol, 1);

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

      newAlloc.AmountQuote += amountQuote * (1 - TakerFee);

      newQuoteAlloc.AmountQuote -= amountQuote;

      returnOrder.AmountQuote = amountQuote;
    }
    else
    {
      amountQuote = order.AmountQuote ?? (decimal)(order.Amount! * price);

      newAlloc.AmountQuote -= amountQuote;

      newQuoteAlloc.AmountQuote += amountQuote * (1 - TakerFee);

      returnOrder.Amount = order.Amount;
    }

    returnOrder.AmountFilled = price == 0 ? 0 : amountQuote / price;
    returnOrder.AmountQuoteFilled = amountQuote;
    returnOrder.FeePaid = amountQuote * TakerFee;

    if (null == curAlloc)
    {
      _curBalance?.AddAllocation(newAlloc);
    }

    if (null == quoteAlloc)
    {
      _curBalance?.AddAllocation(newQuoteAlloc);
    }

    return Task.FromResult(returnOrder);
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

  public Task<IEnumerable<OrderDto>?> SellAllPositions(string? asset = null)
  {
    throw new NotImplementedException();
  }
}

/// <inheritdoc cref="MockExchange"/>
public class SimExchange : MockExchange, IExchange
{
  protected readonly IExchange _instance;

  /// <summary>
  /// <inheritdoc cref="IExchange"/>
  /// </summary>
  public SimExchange(IExchange exchangeService, Balance? curBalance = null)
    : base(
      exchangeService.QuoteSymbol,
      exchangeService.MinOrderSizeInQuote,
      exchangeService.MakerFee,
      exchangeService.TakerFee,
      curBalance ?? exchangeService.GetBalance().GetAwaiter().GetResult())
  {
    _instance = exchangeService;
  }

  public async Task ProcessOrders(IEnumerable<OrderDto> orders)
  {
    foreach (var order in orders)
    {
      await NewOrder(order);
    }
  }

  new public Task<object?> GetCandles(MarketReqDto market, CandleInterval interval, int limit) => _instance.GetCandles(market, interval, limit);
  new public Task<MarketDataDto?> GetMarket(MarketReqDto market) => _instance.GetMarket(market);
  new public Task<decimal> GetPrice(MarketReqDto market) => _instance.GetPrice(market);
}