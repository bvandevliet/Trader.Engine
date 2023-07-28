using TraderEngine.Common.DTOs.Request;
using TraderEngine.Common.DTOs.Response;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Models;

namespace TraderEngine.API.Exchanges;

public interface IExchange
{
  string QuoteSymbol { get; }

  decimal MinimumOrderSize { get; }

  decimal MakerFee { get; }

  decimal TakerFee { get; }

  Task<Balance> GetBalance();

  // TODO: ASIGN TYPE !!
  Task<object> DepositHistory();

  // TODO: ASIGN TYPE !!
  Task<object> WithdrawHistory();

  // TODO: ASIGN TYPE !!
  Task<object> GetCandles(MarketReqDto market, CandleInterval interval, int limit);

  Task<bool> IsTradable(MarketReqDto market);

  Task<decimal> GetPrice(MarketReqDto market);

  Task<OrderDto> NewOrder(OrderReqDto order);

  Task<OrderDto?> GetOrder(string orderId, MarketReqDto? market = null);

  Task<OrderDto?> CancelOrder(string orderId, MarketReqDto? market = null);

  Task<IEnumerable<OrderDto>> GetOpenOrders(MarketReqDto? market = null);

  Task<IEnumerable<OrderDto>> CancelAllOpenOrders(MarketReqDto? market = null);

  Task<IEnumerable<OrderDto>> SellAllPositions(string? baseSymbol = null);
}