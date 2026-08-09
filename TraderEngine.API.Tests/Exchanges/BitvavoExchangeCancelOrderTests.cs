using System.Net;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TraderEngine.API.Exchanges;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Exchanges;

namespace TraderEngine.API.Tests.Exchanges;

/// <summary>
/// Covers <see cref="BitvavoExchange.CancelOrder"/>, which previously threw
/// <see cref="NotImplementedException"/> unconditionally. That exception was swallowed by
/// <c>RebalancingService.VerifyOrderEnded</c>'s catch block, meaning an order that never filled
/// within its polling budget could never actually be cancelled on the live exchange — a silent,
/// live gap rather than a hypothetical one.
/// </summary>
[TestClass]
public class BitvavoExchangeCancelOrderTests
{
  private static readonly ExchangeCredentials _credentials = new("key", "secret");

  private static BitvavoExchange NewExchange(FakeHttpMessageHandler handler)
  {
    var httpClient = new HttpClient(handler) { BaseAddress = new("https://api.bitvavo.com/v2/") };

    return new BitvavoExchange(Substitute.For<ILogger<BitvavoExchange>>(), httpClient, new BitvavoWebSocketConnectionPool(Substitute.For<ILoggerFactory>(), Substitute.For<ILogger<BitvavoWebSocketConnectionPool>>(), new BitvavoRateLimitState()));
  }

  [TestMethod]
  public async Task CancelOrder_Success_IssuesDeleteRequestForOrderIdAndMarket_ReturnsMappedOrder()
  {
    // Arrange
    var handler = new FakeHttpMessageHandler(HttpStatusCode.OK,
      """{"orderId":"abc-123","market":"BTC-EUR","status":"canceled","side":"sell","orderType":"market"}""");

    var exchange = NewExchange(handler);

    // Act
    var result = await exchange.CancelOrder(_credentials, "abc-123", new MarketReqDto("EUR", "BTC"));

    // Assert
    Assert.IsNotNull(result);
    Assert.AreEqual("abc-123", result.Id);
    Assert.AreEqual(OrderStatus.Canceled, result.Status);
    Assert.AreEqual(OrderSide.Sell, result.Side);
    Assert.AreEqual(new MarketReqDto("EUR", "BTC"), result.Market);

    Assert.IsNotNull(handler.LastRequest);
    Assert.AreEqual(HttpMethod.Delete, handler.LastRequest.Method);
    Assert.Contains("order?orderId=abc-123&market=BTC-EUR", handler.LastRequest.RequestUri!.PathAndQuery);
  }

  [TestMethod]
  public async Task CancelOrder_ExchangeReturnsError_ReturnsNull_DoesNotThrow()
  {
    // Arrange
    var handler = new FakeHttpMessageHandler(HttpStatusCode.NotFound, """{"errorCode":"240","error":"Order not found."}""");

    var exchange = NewExchange(handler);

    // Act
    var result = await exchange.CancelOrder(_credentials, "does-not-exist", new MarketReqDto("EUR", "BTC"));

    // Assert
    Assert.IsNull(result);
  }
}
