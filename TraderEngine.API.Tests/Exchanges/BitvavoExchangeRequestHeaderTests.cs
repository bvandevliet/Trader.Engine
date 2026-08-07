using System.Net;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TraderEngine.API.Exchanges;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.Exchanges;

namespace TraderEngine.API.Tests.Exchanges;

/// <summary>
/// Covers <see cref="BitvavoExchange"/>'s <c>bitvavo-access-window</c> request header, which
/// previously carried an unintentional trailing space (<c>"10000 "</c>), inconsistent with the
/// numeric <c>window</c> field <see cref="BitvavoWebSocketClient"/> sends on its WebSocket
/// authenticate message. Both now derive from the shared <see cref="BitvavoDefaults.AccessWindowMs"/>.
/// </summary>
[TestClass]
public class BitvavoExchangeRequestHeaderTests
{
  private static readonly ExchangeCredentials _credentials = new("key", "secret");

  [TestMethod]
  public async Task GetPrice_RequestHeaders_AccessWindowHasExactValue_NoTrailingSpace()
  {
    // Arrange
    var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, """{"market":"BTC-EUR","price":"50000"}""");
    var httpClient = new HttpClient(handler) { BaseAddress = new("https://api.bitvavo.com/v2/") };
    var exchange = new BitvavoExchange(Substitute.For<ILogger<BitvavoExchange>>(), httpClient, new BitvavoWebSocketConnectionPool(Substitute.For<ILoggerFactory>(), Substitute.For<ILogger<BitvavoWebSocketConnectionPool>>(), new BitvavoRateLimitState()));

    // Act
    _ = await exchange.GetPrice(_credentials, new MarketReqDto("EUR", "BTC"));

    // Assert
    Assert.IsNotNull(handler.LastRequest);
    var windowValues = handler.LastRequest.Headers.GetValues("bitvavo-access-window").ToArray();
    Assert.AreEqual(1, windowValues.Length);
    Assert.AreEqual("10000", windowValues[0]);
  }
}
