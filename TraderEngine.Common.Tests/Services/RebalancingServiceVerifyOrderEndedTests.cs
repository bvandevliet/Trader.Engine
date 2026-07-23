using Microsoft.Extensions.Logging.Abstractions;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Services;
using TraderEngine.Common.Tests.Exchanges;

namespace TraderEngine.Common.Tests.Services;

[TestClass]
public class RebalancingServiceVerifyOrderEndedTests
{
  private static readonly IRebalancingService _service = new RebalancingService(NullLogger<RebalancingService>.Instance);

  private static readonly MarketReqDto _market = new("EUR", "BTC");

  // ── Fast paths, no polling delay incurred ──────────────────────────────────

  [TestMethod]
  public async Task VerifyOrderEnded_OrderAlreadyEnded_ReturnsImmediately()
  {
    // Arrange
    var exchange = new ScriptedExchange();

    var order = new OrderDto { Id = "abc", Market = _market, Status = OrderStatus.Filled };

    // Act
    var result = await _service.VerifyOrderEnded(exchange, order);

    // Assert
    Assert.AreSame(order, result);
    Assert.AreEqual(0, exchange.GetOrderCalls.Count);
    Assert.AreEqual(0, exchange.CancelOrderCalls.Count);
  }

  [TestMethod]
  public async Task VerifyOrderEnded_NullOrderId_ReturnsImmediately_EvenIfNotEnded()
  {
    // Arrange
    // An order without an Id can never be polled or cancelled — must short-circuit regardless
    // of its status.
    var exchange = new ScriptedExchange();

    var order = new OrderDto { Id = null, Market = _market, Status = OrderStatus.New };

    // Act
    var result = await _service.VerifyOrderEnded(exchange, order);

    // Assert
    Assert.AreSame(order, result);
    Assert.AreEqual(0, exchange.GetOrderCalls.Count);
    Assert.AreEqual(0, exchange.CancelOrderCalls.Count);
  }

  // ── Polling paths (each incurs one real ~1s delay per check) ───────────────

  [TestMethod]
  public async Task VerifyOrderEnded_EndsWithinChecks_ReturnsUpdatedOrder_DoesNotCancel()
  {
    // Arrange
    var exchange = new ScriptedExchange();

    var updatedOrder = new OrderDto { Id = "abc", Market = _market, Status = OrderStatus.Filled };
    exchange.EnqueueGetOrderResponse(updatedOrder);

    var order = new OrderDto { Id = "abc", Market = _market, Status = OrderStatus.New };

    // Act
    var result = await _service.VerifyOrderEnded(exchange, order, cancel: true, checks: 3);

    // Assert
    Assert.AreSame(updatedOrder, result);
    Assert.AreEqual(1, exchange.GetOrderCalls.Count);
    Assert.AreEqual(0, exchange.CancelOrderCalls.Count);
  }

  [TestMethod]
  public async Task VerifyOrderEnded_NeverEndsWithinChecks_CancelsOrder()
  {
    // Arrange
    var exchange = new ScriptedExchange
    {
      CancelOrderResponse = new OrderDto { Id = "abc", Market = _market, Status = OrderStatus.Canceled },
    };

    // GetOrder never reports the order as ended.
    exchange.EnqueueGetOrderResponse(new OrderDto { Id = "abc", Market = _market, Status = OrderStatus.New });

    var order = new OrderDto { Id = "abc", Market = _market, Status = OrderStatus.New };

    // Act
    var result = await _service.VerifyOrderEnded(exchange, order, cancel: true, checks: 1);

    // Assert
    Assert.AreEqual(1, exchange.CancelOrderCalls.Count);
    Assert.AreEqual(("abc", _market), exchange.CancelOrderCalls[0]);
    Assert.AreEqual(OrderStatus.Canceled, result.Status);
  }

  [TestMethod]
  public async Task VerifyOrderEnded_CancelFalse_DoesNotCancel_EvenIfNeverEnds()
  {
    // Arrange
    var exchange = new ScriptedExchange();

    exchange.EnqueueGetOrderResponse(new OrderDto { Id = "abc", Market = _market, Status = OrderStatus.New });

    var order = new OrderDto { Id = "abc", Market = _market, Status = OrderStatus.New };

    // Act
    var result = await _service.VerifyOrderEnded(exchange, order, cancel: false, checks: 1);

    // Assert
    Assert.AreEqual(0, exchange.CancelOrderCalls.Count);
    Assert.AreEqual(OrderStatus.New, result.Status);
  }

  [TestMethod]
  public async Task VerifyOrderEnded_CancelThrows_SwallowsExceptionAndReturnsLastKnownOrder()
  {
    // Arrange
    var exchange = new ScriptedExchange
    {
      ThrowOnCancelOrder = true,
    };

    var lastKnownOrder = new OrderDto { Id = "abc", Market = _market, Status = OrderStatus.New };
    exchange.EnqueueGetOrderResponse(lastKnownOrder);

    var order = new OrderDto { Id = "abc", Market = _market, Status = OrderStatus.New };

    // Act
    var result = await _service.VerifyOrderEnded(exchange, order, cancel: true, checks: 1);

    // Assert — no exception propagates, and the last polled (still open) order is returned
    // since the failed cancellation never gets to reassign it.
    Assert.AreEqual(1, exchange.CancelOrderCalls.Count);
    Assert.AreSame(lastKnownOrder, result);
  }

  [TestMethod]
  public async Task VerifyOrderEnded_GetOrderReturnsNull_KeepsPreviousOrderAndContinues()
  {
    // Arrange
    // A transient lookup failure (null) must not crash the poll loop — it falls back to the
    // last known order via the `?? order` coalesce and keeps polling.
    var exchange = new ScriptedExchange();

    exchange.EnqueueGetOrderResponse(null);

    var order = new OrderDto { Id = "abc", Market = _market, Status = OrderStatus.New };

    // Act
    var result = await _service.VerifyOrderEnded(exchange, order, cancel: false, checks: 1);

    // Assert
    Assert.AreEqual(1, exchange.GetOrderCalls.Count);
    Assert.AreEqual(OrderStatus.New, result.Status);
  }
}
