using Microsoft.Extensions.Logging.Abstractions;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Exchanges;
using TraderEngine.Common.Results;
using TraderEngine.Common.Services;
using TraderEngine.Common.Tests.Exchanges;

namespace TraderEngine.Common.Tests.Services;

/// <summary>
/// Covers <see cref="RebalancingService.Rebalance(IExchange, ExchangeCredentials, IEnumerable{OrderReqDto}, string)"/>'s
/// WebSocket-session-scoping wrapper: a push-capable exchange gets exactly one session begun and
/// disposed per <c>Rebalance</c> call, regardless of how many orders are placed inside it; an
/// exchange without that capability is entirely unaffected.
/// </summary>
[TestClass]
public class RebalancingServiceSessionScopeTests
{
  private static readonly IRebalancingService _service = new RebalancingService(NullLogger<RebalancingService>.Instance);

  private static readonly ExchangeCredentials _credentials = new("test-key", "test-secret");

  private static readonly MarketReqDto _btc = new("EUR", "BTC");

  private static OrderDto FilledOrder(string id) => new()
  {
    Id = id,
    Market = _btc,
    Side = OrderSide.Sell,
    Type = OrderType.Market,
    Status = OrderStatus.Filled,
    Amount = 1,
    AmountFilled = 1,
    AmountRemaining = 0,
  };

  [TestMethod]
  public async Task Rebalance_ExchangeImplementsOrderNotifications_BeginsAndDisposesSessionExactlyOnce()
  {
    // Arrange
    var inner = new ScriptedExchange { MinOrderSizeInQuote = 1 };
    inner.EnqueueNewOrderResponse(Result<OrderDto, ExchangeErrCodeEnum>.Success(FilledOrder("1")));
    inner.EnqueueNewOrderResponse(Result<OrderDto, ExchangeErrCodeEnum>.Success(FilledOrder("2")));

    var exchange = new ScriptedOrderNotificationExchange(inner);

    var orders = new[]
    {
      new OrderReqDto { Market = _btc, Side = OrderSide.Sell, Type = OrderType.Market, Amount = 1 },
      new OrderReqDto { Market = _btc, Side = OrderSide.Sell, Type = OrderType.Market, Amount = 1 },
    };

    // Act
    var results = await _service.Rebalance(exchange, _credentials, orders, "Test");

    // Assert
    Assert.AreEqual(2, results.Length);
    Assert.AreEqual(1, exchange.BeginSessionCallCount);
    Assert.AreEqual(1, exchange.SessionDisposeCallCount);
  }

  [TestMethod]
  public async Task Rebalance_ExchangeDoesNotImplementOrderNotifications_NeverCallsSessionMethods()
  {
    // Arrange
    var exchange = new ScriptedExchange { MinOrderSizeInQuote = 1 };
    exchange.EnqueueNewOrderResponse(Result<OrderDto, ExchangeErrCodeEnum>.Success(FilledOrder("1")));

    var orders = new[]
    {
      new OrderReqDto { Market = _btc, Side = OrderSide.Sell, Type = OrderType.Market, Amount = 1 },
    };

    // Act
    var results = await _service.Rebalance(exchange, _credentials, orders, "Test");

    // Assert: this is really just confirming Rebalance completes normally against a plain
    // IExchange (no IExchangeOrderNotifications) — there is no session API on it to assert against,
    // which is the point.
    Assert.AreEqual(1, results.Length);
  }
}
