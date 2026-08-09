using System.Net;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TraderEngine.API.Exchanges;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Exchanges;

namespace TraderEngine.API.Tests.Exchanges;

/// <summary>
/// Covers <see cref="BitvavoExchange.GetOpenOrders"/>, which previously threw
/// <see cref="NotImplementedException"/> unconditionally.
/// </summary>
[TestClass]
public class BitvavoExchangeGetOpenOrdersTests
{
  private static readonly ExchangeCredentials _credentials = new("key", "secret");

  private static BitvavoExchange NewExchange(FakeHttpMessageHandler handler)
  {
    var httpClient = new HttpClient(handler) { BaseAddress = new("https://api.bitvavo.com/v2/") };

    return new BitvavoExchange(Substitute.For<ILogger<BitvavoExchange>>(), httpClient, new BitvavoWebSocketConnectionPool(Substitute.For<ILoggerFactory>(), Substitute.For<ILogger<BitvavoWebSocketConnectionPool>>(), new BitvavoRateLimitState()));
  }

  [TestMethod]
  public async Task GetOpenOrders_NoMarketFilter_RequestsOrdersOpenEndpoint_NoQueryString()
  {
    // Arrange
    var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "[]");

    var exchange = NewExchange(handler);

    // Act
    _ = await exchange.GetOpenOrders(_credentials);

    // Assert
    Assert.IsNotNull(handler.LastRequest);
    Assert.AreEqual(HttpMethod.Get, handler.LastRequest.Method);
    Assert.AreEqual("/v2/ordersOpen", handler.LastRequest.RequestUri!.AbsolutePath);
    Assert.AreEqual(string.Empty, handler.LastRequest.RequestUri!.Query);
  }

  [TestMethod]
  public async Task GetOpenOrders_WithMarketFilter_AppendsMarketQueryParam()
  {
    // Arrange
    var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "[]");

    var exchange = NewExchange(handler);

    // Act
    _ = await exchange.GetOpenOrders(_credentials, new MarketReqDto("EUR", "BTC"));

    // Assert
    Assert.IsNotNull(handler.LastRequest);
    Assert.AreEqual("?market=BTC-EUR", handler.LastRequest.RequestUri!.Query);
  }

  [TestMethod]
  public async Task GetOpenOrders_SuccessResponse_MapsOrdersViaApiMapper()
  {
    // Arrange
    var handler = new FakeHttpMessageHandler(HttpStatusCode.OK,
      """[{"orderId":"abc-123","market":"BTC-EUR","status":"new","side":"sell","orderType":"limit"}]""");

    var exchange = NewExchange(handler);

    // Act
    var result = (await exchange.GetOpenOrders(_credentials))?.ToList();

    // Assert
    Assert.IsNotNull(result);
    Assert.AreEqual(1, result.Count);
    Assert.AreEqual("abc-123", result[0].Id);
    Assert.AreEqual(OrderStatus.New, result[0].Status);
    Assert.AreEqual(OrderSide.Sell, result[0].Side);
    Assert.AreEqual(new MarketReqDto("EUR", "BTC"), result[0].Market);
  }

  [TestMethod]
  public async Task GetOpenOrders_EmptyArrayResponse_ReturnsEmptyEnumerable_NotNull()
  {
    // Arrange
    var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "[]");

    var exchange = NewExchange(handler);

    // Act
    var result = await exchange.GetOpenOrders(_credentials);

    // Assert
    Assert.IsNotNull(result);
    Assert.AreEqual(0, result.Count());
  }

  [TestMethod]
  public async Task GetOpenOrders_NonSuccessStatusCode_ReturnsNull()
  {
    // Arrange
    var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, """{"errorCode":"999","error":"Unexpected."}""");

    var exchange = NewExchange(handler);

    // Act
    var result = await exchange.GetOpenOrders(_credentials);

    // Assert
    Assert.IsNull(result);
  }

  [TestMethod]
  public async Task GetOpenOrders_MalformedResponseBody_Throws()
  {
    // Arrange
    var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "not json");

    var exchange = NewExchange(handler);

    // Act & Assert
    await Assert.ThrowsExactlyAsync<System.Text.Json.JsonException>(() => exchange.GetOpenOrders(_credentials));
  }
}
