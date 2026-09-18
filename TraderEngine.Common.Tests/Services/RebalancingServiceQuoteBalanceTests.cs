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
/// Covers <see cref="RebalancingService.Rebalance(IExchange, IEnumerable{OrderReqDto}, string)"/>'s
/// use of <see cref="IExchangeQuoteBalance"/>'s lightweight quote-only balance path (both to seed
/// the run's initial available budget and, since a run with zero sells still reaches it, the
/// interleaved-execution reconciliation pass), instead of a full <see cref="IExchange.GetBalance"/>
/// call, which prices every held asset even though only the quote-currency total is ever used here.
/// </summary>
[TestClass]
public class RebalancingServiceQuoteBalanceTests
{
  private static readonly IRebalancingService
    _service = new RebalancingService(NullLogger<RebalancingService>.Instance);

  private static readonly ExchangeCredentials _credentials = new("test-key", "test-secret");

  private static readonly MarketReqDto _btc = new("EUR", "BTC");

  private static OrderReqDto[] SingleBuyOrder()
  {
    return
    [
      new OrderReqDto
      {
        Market = _btc,
        Side = OrderSide.Buy,
        Type = OrderType.Market,
        AmountQuote = 100,
      },
    ];
  }

  private static OrderDto FilledBuyOrder()
  {
    return new OrderDto
    {
      Id = "buy-1",
      Market = _btc,
      Side = OrderSide.Buy,
      Type = OrderType.Market,
      Status = OrderStatus.Filled,
      AmountFilled = 1,
      AmountQuoteFilled = 100,
      AmountRemaining = 0,
    };
  }

  [TestMethod]
  public async Task Rebalance_GivenOrders_UsesLightweightQuoteBalance_WhenExchangeSupportsIt()
  {
    // Arrange
    var exchange = new ScriptedExchange { MinOrderSizeInQuote = 1 };

    exchange.EnqueueNewOrderResponse(Result<OrderDto, ExchangeErrCodeEnum>.Success(FilledBuyOrder()));

    // Act
    var results = await _service.Rebalance(exchange, _credentials, SingleBuyOrder(), "Test");

    // Assert
    Assert.AreEqual(1, results.Length);
    Assert.AreEqual(OrderStatus.Filled, results[0].Status);

    // Once to seed the run's initial available budget, once more for the interleaved-execution
    // reconciliation pass (which still runs even with zero sell legs) — neither needed to price
    // every held asset via a full GetBalance call.
    Assert.AreEqual(2, exchange.GetQuoteBalanceAvailableCalls);
    Assert.AreEqual(0, exchange.GetBalanceCalls);
  }

  [TestMethod]
  public async Task Rebalance_GivenOrders_FallsBackToFullBalance_WhenLightweightQuoteBalanceFails()
  {
    // Arrange
    var exchange = new ScriptedExchange
    {
      MinOrderSizeInQuote = 1,
      QuoteBalanceAvailableResponse = Result<decimal, ExchangeErrCodeEnum>.Failure(0, ExchangeErrCodeEnum.Other),
    };

    exchange.EnqueueNewOrderResponse(Result<OrderDto, ExchangeErrCodeEnum>.Success(FilledBuyOrder()));

    // Act
    var results = await _service.Rebalance(exchange, _credentials, SingleBuyOrder(), "Test");

    // Assert — the run still succeeds, using ScriptedExchange's default (ample) BalanceResponse
    // via the full GetBalance fallback.
    Assert.AreEqual(1, results.Length);
    Assert.AreEqual(OrderStatus.Filled, results[0].Status);

    Assert.AreEqual(2, exchange.GetQuoteBalanceAvailableCalls);
    Assert.AreEqual(2, exchange.GetBalanceCalls);
  }
}
