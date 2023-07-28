using TraderEngine.Common.DTOs.Request;
using TraderEngine.Common.DTOs.Response;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Models;

namespace TraderEngine.API.Exchanges;

/// <inheritdoc cref="IExchange"/>
public class MockExchange : IExchange
{
  protected Balance curBalance = null!;

  public string QuoteSymbol { get; protected set; } = "EUR";

  public decimal MinimumOrderSize { get; protected set; }

  public decimal MakerFee { get; protected set; }

  public decimal TakerFee { get; protected set; }

  protected void Init(Balance? curBalance)
  {
    if (null != curBalance)
    {
      this.curBalance = curBalance;
    }
    else
    {
      decimal deposit = 1000;

      this.curBalance = new(QuoteSymbol);

      this.curBalance.AddAllocation(new(market: new MarketReqDto(QuoteSymbol, baseSymbol: "EUR"), price: 000001, amount: .05m * deposit));
      this.curBalance.AddAllocation(new(market: new MarketReqDto(QuoteSymbol, baseSymbol: "BTC"), price: 18_000, amount: .40m * deposit / 15_000));
      this.curBalance.AddAllocation(new(market: new MarketReqDto(QuoteSymbol, baseSymbol: "ETH"), price: 01_610, amount: .30m * deposit / 01_400));
      this.curBalance.AddAllocation(new(market: new MarketReqDto(QuoteSymbol, baseSymbol: "BNB"), price: 000306, amount: .25m * deposit / 000340));
      //                                                                                                                 100%
    }
  }

  /// <summary>
  /// <inheritdoc cref="IExchange"/>
  /// </summary>
  /// <param name="curBalance"><inheritdoc cref="Balance"/></param>
  public MockExchange(Balance? curBalance = null)
  {
    Init(curBalance);
  }

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
    Balance? curBalance = null)
  {
    QuoteSymbol = quoteSymbol;
    MinimumOrderSize = minimumOrderSize;
    MakerFee = makerFee;
    TakerFee = takerFee;

    Init(curBalance);
  }

  /// <summary>
  /// <inheritdoc cref="IExchange"/>
  /// </summary>
  /// <param name="exchangeService">Instance of the exchange service to base this mock instance on.</param>
  /// <param name="curBalance"><inheritdoc cref="Balance"/></param>
  public MockExchange(IExchange exchangeService, Balance? curBalance = null)
    : this(
      exchangeService.QuoteSymbol,
      exchangeService.MinimumOrderSize,
      exchangeService.MakerFee,
      exchangeService.TakerFee,
      // Override current balance with the actual one from the underlying exchange service if none given.
      curBalance ?? exchangeService.GetBalance().Result)
  {
  }

  /// <summary>
  /// If no initial <see cref="Balance"/> was given,
  /// this returns a drifted <see cref="Balance"/> that had a initial value of €1000 and initially allocated as follows:
  /// <br/>
  /// EUR :  5 %
  /// <br/>
  /// BTC : 40 %
  /// <br/>
  /// ETH : 30 %
  /// <br/>
  /// BNB : 25 %
  /// </summary>
  /// <returns></returns>
  public Task<Balance> GetBalance()
  {
    return Task.FromResult(curBalance);
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
    Allocation? curAlloc = curBalance.GetAllocation(order.Market.BaseSymbol);

    Allocation newAlloc = curAlloc ?? new(order.Market);

    Allocation? quoteAlloc = curBalance.GetAllocation(QuoteSymbol);

    Allocation newQuoteAlloc = quoteAlloc ?? new(QuoteSymbol, QuoteSymbol, 1);

    var returnOrder = new OrderDto()
    {
      Market = order.Market,
      Side = order.Side,
      Status = OrderStatus.Filled
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
      curBalance.AddAllocation(newAlloc);
    }

    if (null == quoteAlloc)
    {
      curBalance.AddAllocation(newQuoteAlloc);
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
  /// <summary>
  /// The underlying exchange service instance this mock instance is based on.
  /// </summary>
  public T ExchangeService { get; }

  /// <summary>
  /// <inheritdoc cref="IExchange"/>
  /// </summary>
  public MockExchange(T exchangeService) : base()
  {
    ExchangeService = exchangeService;

    QuoteSymbol = ExchangeService.QuoteSymbol;
    MinimumOrderSize = ExchangeService.MinimumOrderSize;
    MakerFee = ExchangeService.MakerFee;
    TakerFee = ExchangeService.TakerFee;

    // Override current balance with the actual one from the underlying exchange service.
    try
    {
      curBalance = ExchangeService.GetBalance().Result;
    }
    catch (NotImplementedException)
    {
      //Init(null);
    }
  }
}