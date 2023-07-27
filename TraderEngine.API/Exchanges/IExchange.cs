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
  Task<object> GetCandles(MarketDto market, CandleInterval interval, int limit);

  Task<bool> IsTradable(MarketDto market);

  Task<decimal> GetPrice(MarketDto market);

  Task<Order> NewOrder(OrderDto order);

  Task<Order?> GetOrder(string orderId, MarketDto? market = null);

  Task<Order?> CancelOrder(string orderId, MarketDto? market = null);

  Task<IEnumerable<Order>> GetOpenOrders(MarketDto? market = null);

  Task<IEnumerable<Order>> CancelAllOpenOrders(MarketDto? market = null);

  Task<IEnumerable<Order>> SellAllPositions(string? baseSymbol = null);
}