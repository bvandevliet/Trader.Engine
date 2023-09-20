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

  public decimal MinimumOrderSize { get; }

  public decimal MakerFee { get; }

  public decimal TakerFee { get; }

  public string ApiKey { get; set; } = string.Empty;

  public string ApiSecret { get; set; } = string.Empty;

  /// <summary>
  /// <inheritdoc cref="IExchange"/>
  /// </summary>
  /// <param name="quoteSymbol"><inheritdoc cref="QuoteSymbol"/></param>
  /// <param name="minimumOrderSize"><inheritdoc cref="MinimumOrderSize"/></param>
  /// <param name="makerFee"><inheritdoc cref="MakerFee"/></param>
  /// <param name="takerFee"><inheritdoc cref="TakerFee"/></param>
  /// <param name="curBalance"><inheritdoc cref="Balance"/></param>
  public MockExchange(
    string quoteSymbol,
    decimal minimumOrderSize,
    decimal makerFee,
    decimal takerFee,
    Balance curBalance)
  {
    QuoteSymbol = quoteSymbol;
    MinimumOrderSize = minimumOrderSize;
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

  public Task<object> DepositHistory()
  {
    throw new NotImplementedException();
  }

  public Task<object> WithdrawHistory()
  {
    throw new NotImplementedException();
  }

  public Task<object> GetCandles(MarketReqDto market, CandleInterval interval, int limit)
  {
    throw new NotImplementedException();
  }

  public Task<bool> IsTradable(MarketReqDto market)
  {
    return Task.FromResult(true);
  }

  public Task<decimal> GetPrice(MarketReqDto market)
  {
    throw new NotImplementedException();
  }

  public Task<OrderDto> NewOrder(OrderReqDto order)
  {
    Allocation? curAlloc = _curBalance.GetAllocation(order.Market.BaseSymbol);

    Allocation newAlloc = curAlloc ?? new(order.Market);

    Allocation? quoteAlloc = _curBalance.GetAllocation(QuoteSymbol);

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

    decimal amountQuote;

    if (order.Side == OrderSide.Buy)
    {
      amountQuote = order.AmountQuote ?? (decimal)(order.Amount * curAlloc!.Price);

      newAlloc.AmountQuote += amountQuote;

      newQuoteAlloc.AmountQuote -= amountQuote;

      returnOrder.AmountQuote = amountQuote;
    }
    else
    {
      amountQuote = order.AmountQuote ?? (decimal)(order.Amount * curAlloc!.Price);

      newAlloc.AmountQuote -= amountQuote;

      newQuoteAlloc.AmountQuote += amountQuote;

      returnOrder.Amount = order.Amount;
    }

    returnOrder.FeePaid = amountQuote * TakerFee;

    if (null == curAlloc)
    {
      _curBalance.AddAllocation(newAlloc);
    }

    if (null == quoteAlloc)
    {
      _curBalance.AddAllocation(newQuoteAlloc);
    }

    return Task.FromResult(returnOrder);
  }

  public Task<OrderDto?> GetOrder(string orderId, MarketReqDto? market = null)
  {
    throw new NotImplementedException();
  }

  public Task<OrderDto?> CancelOrder(string orderId, MarketReqDto? market = null)
  {
    throw new NotImplementedException();
  }

  public Task<IEnumerable<OrderDto>> GetOpenOrders(MarketReqDto? market = null)
  {
    throw new NotImplementedException();
  }

  public Task<IEnumerable<OrderDto>> CancelAllOpenOrders(MarketReqDto? market = null)
  {
    return Task.FromResult(new List<OrderDto>().AsEnumerable());
  }

  public Task<IEnumerable<OrderDto>> SellAllPositions(string? asset = null)
  {
    throw new NotImplementedException();
  }
}

/// <inheritdoc cref="MockExchange"/>
public class MockExchange<T> : MockExchange, IExchange where T : class, IExchange
{
  protected readonly IExchange _instance;

  /// <summary>
  /// <inheritdoc cref="IExchange"/>
  /// </summary>
  public MockExchange(T exchangeService, Balance? curBalance = null)
    : base(
      exchangeService.QuoteSymbol,
      exchangeService.MinimumOrderSize,
      exchangeService.MakerFee,
      exchangeService.TakerFee,
      curBalance ?? exchangeService.GetBalance().Result)
  {
    _instance = exchangeService;
  }

  new public Task<decimal> GetPrice(MarketReqDto market) => _instance.GetPrice(market);
}