using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TraderEngine.API.Exchanges;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.Exchanges;

namespace TraderEngine.API.Tests.Exchanges;

/// <summary>
/// Covers <see cref="BitvavoExchange.GetTakerFee"/>, which replaced a hardcoded 0.0025m constant
/// (see CLAUDE.md's "Bitvavo integration: known future improvements" entry on this fix) after a
/// live rebalance run dropped a buy order on insufficient balance because the account's actual
/// realized fee on some markets ran above that assumed flat rate. Calls Bitvavo's per-market
/// <c>GET /account/fees?market=X</c> endpoint (account-wide default when no market is given), and
/// caches each result individually per <see cref="BitvavoExchange"/> instance (itself DI-scoped
/// per request/rebalance run, so no cross-account leakage risk the way the shared public-data
/// cache would have).
/// </summary>
[TestClass]
public class BitvavoExchangeGetTakerFeeTests
{
  private static readonly ExchangeCredentials _credentials = new("key", "secret");

  private const string _categoryAFeesBody =
    """
    {"tier":"0","volume":"10000.00","taker":"0.0025","maker":"0.0015"}
    """;

  private const string _categoryBFeesBody =
    """
    {"tier":"0","volume":"500.00","taker":"0.0005","maker":"0.0005"}
    """;

  private static BitvavoExchange NewExchange(FakeHttpMessageHandler handler)
  {
    var httpClient = new HttpClient(handler) { BaseAddress = new("https://api.bitvavo.com/v2/") };

    return new BitvavoExchange(
      Substitute.For<ILogger<BitvavoExchange>>(),
      httpClient,
      new BitvavoWebSocketConnectionPool(Substitute.For<ILoggerFactory>(), Substitute.For<ILogger<BitvavoWebSocketConnectionPool>>(), new BitvavoRateLimitState()),
      new MemoryCache(new MemoryCacheOptions()));
  }

  [TestMethod]
  public async Task GetTakerFee_WithMarket_RequestsPerMarketEndpoint_WithQueryString()
  {
    // Arrange
    var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, _categoryBFeesBody);

    var exchange = NewExchange(handler);

    // Act
    var fee = await exchange.GetTakerFee(_credentials, new MarketReqDto("USDC", "BTC"));

    // Assert — filtered to just this market, not the account-wide default.
    Assert.IsNotNull(handler.LastRequest);
    Assert.AreEqual(HttpMethod.Get, handler.LastRequest.Method);
    Assert.AreEqual("/v2/account/fees", handler.LastRequest.RequestUri!.AbsolutePath);
    Assert.AreEqual("?market=BTC-USDC", handler.LastRequest.RequestUri!.Query);

    Assert.AreEqual(0.0005m, fee);
  }

  [TestMethod]
  public async Task GetTakerFee_NoMarket_RequestsAccountDefaultEndpoint_NoQueryString()
  {
    // Arrange
    var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, _categoryAFeesBody);

    var exchange = NewExchange(handler);

    // Act
    var fee = await exchange.GetTakerFee(_credentials);

    // Assert
    Assert.IsNotNull(handler.LastRequest);
    Assert.AreEqual("/v2/account/fees", handler.LastRequest.RequestUri!.AbsolutePath);
    Assert.AreEqual("", handler.LastRequest.RequestUri!.Query);

    Assert.AreEqual(0.0025m, fee);
  }

  [TestMethod]
  public async Task GetTakerFee_SecondCallSameMarket_ReusesPerInstanceCache_NoNewHttpRequest()
  {
    // Arrange
    var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, _categoryAFeesBody);

    var exchange = NewExchange(handler);

    // Act
    _ = await exchange.GetTakerFee(_credentials, new MarketReqDto("EUR", "BTC"));
    _ = await exchange.GetTakerFee(_credentials, new MarketReqDto("EUR", "BTC"));

    // Assert
    Assert.AreEqual(1, handler.RequestCount);
  }

  [TestMethod]
  public async Task GetTakerFee_DifferentMarket_TriggersSeparateFetch_CanReturnADifferentRate()
  {
    // Arrange — this is the whole point of the fix: different markets can carry different fee
    // categories/rates, so a cached result for one market must not answer for another.
    var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, _categoryAFeesBody);

    var exchange = NewExchange(handler);

    // Act
    var eurFee = await exchange.GetTakerFee(_credentials, new MarketReqDto("EUR", "BTC"));

    handler.SetResponse(HttpStatusCode.OK, _categoryBFeesBody);
    var usdcFee = await exchange.GetTakerFee(_credentials, new MarketReqDto("USDC", "BTC"));

    // Assert
    Assert.AreEqual(2, handler.RequestCount);
    Assert.AreEqual(0.0025m, eurFee);
    Assert.AreEqual(0.0005m, usdcFee);
  }

  [TestMethod]
  public async Task GetTakerFee_FetchFails_FallsBackToDocumentedDefault_DoesNotThrow()
  {
    // Arrange — a live-fee-lookup outage must never block a rebalance run outright; it should
    // degrade to the documented Category A default instead.
    var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, """{"errorCode":"999","error":"Unexpected."}""");

    var exchange = NewExchange(handler);

    // Act
    var fee = await exchange.GetTakerFee(_credentials, new MarketReqDto("EUR", "BTC"));

    // Assert
    Assert.AreEqual(0.0025m, fee);
  }

  [TestMethod]
  public async Task GetTakerFee_MalformedResponse_FallsBackToDocumentedDefault_DoesNotThrow()
  {
    // Arrange
    var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, """{"not":"the expected shape"}""");

    var exchange = NewExchange(handler);

    // Act
    var fee = await exchange.GetTakerFee(_credentials, new MarketReqDto("EUR", "BTC"));

    // Assert
    Assert.AreEqual(0.0025m, fee);
  }

  [TestMethod]
  public async Task GetMakerFee_ReturnsMakerRateFromTheSameResponseShape()
  {
    // Arrange
    var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, _categoryAFeesBody);

    var exchange = NewExchange(handler);

    // Act
    var fee = await exchange.GetMakerFee(_credentials, new MarketReqDto("EUR", "BTC"));

    // Assert
    Assert.AreEqual(0.0015m, fee);
  }

  [TestMethod]
  public async Task GetMakerFee_FetchFails_FallsBackToDocumentedDefault_DoesNotThrow()
  {
    // Arrange
    var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, """{"errorCode":"999","error":"Unexpected."}""");

    var exchange = NewExchange(handler);

    // Act
    var fee = await exchange.GetMakerFee(_credentials, new MarketReqDto("EUR", "BTC"));

    // Assert
    Assert.AreEqual(0.0015m, fee);
  }

  [TestMethod]
  public async Task GetTakerFee_ThenGetMakerFee_SameMarket_ReusesTheSameCachedFetch_OneHttpRequestTotal()
  {
    // Arrange — GetTakerFee and GetMakerFee both read from the same per-market cache entry (the
    // account/fees response already carries both rates in one payload), so asking for both must
    // not double the number of HTTP calls made per market.
    var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, _categoryAFeesBody);

    var exchange = NewExchange(handler);

    // Act
    var taker = await exchange.GetTakerFee(_credentials, new MarketReqDto("EUR", "BTC"));
    var maker = await exchange.GetMakerFee(_credentials, new MarketReqDto("EUR", "BTC"));

    // Assert
    Assert.AreEqual(1, handler.RequestCount);
    Assert.AreEqual(0.0025m, taker);
    Assert.AreEqual(0.0015m, maker);
  }
}
