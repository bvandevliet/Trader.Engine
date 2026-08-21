using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TraderEngine.API.Exchanges;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Exchanges;

namespace TraderEngine.API.Tests.Exchanges;

/// <summary>
/// Covers <see cref="BitvavoExchange.GetMarket"/>, which calls Bitvavo's per-market
/// <c>GET /markets?market=X</c> endpoint and caches each market's result individually (via
/// <see cref="IMemoryCache"/>, 10s TTL) rather than caching one bulk <c>GET /markets</c> fetch —
/// a market absent from an unfiltered bulk response is ambiguous, whereas the single-market
/// endpoint's errorCode 205 unambiguously means "not found" for the exact market asked about.
/// </summary>
[TestClass]
public class BitvavoExchangeGetMarketTests
{
  private static readonly ExchangeCredentials _credentials = new("key", "secret");

  private const string _btcMarketBody =
    """
    {"market":"BTC-EUR","status":"trading","minOrderInQuoteAsset":"5","minOrderInBaseAsset":"0.0001"}
    """;

  private const string _adaMarketBody =
    """
    {"market":"ADA-EUR","status":"trading","minOrderInQuoteAsset":"5","minOrderInBaseAsset":"31"}
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
  public async Task GetMarket_FirstCall_RequestsSingleMarketEndpoint_WithQueryString()
  {
    // Arrange
    var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, _btcMarketBody);

    var exchange = NewExchange(handler);

    // Act
    var result = await exchange.GetMarket(_credentials, new MarketReqDto("EUR", "BTC"));

    // Assert — filtered to just this market, not every market.
    Assert.IsNotNull(handler.LastRequest);
    Assert.AreEqual(HttpMethod.Get, handler.LastRequest.Method);
    Assert.AreEqual("/v2/markets", handler.LastRequest.RequestUri!.AbsolutePath);
    Assert.AreEqual("?market=BTC-EUR", handler.LastRequest.RequestUri!.Query);

    Assert.IsNotNull(result);
    Assert.AreEqual(MarketStatus.Trading, result.Status);
    Assert.AreEqual(5m, result.MinOrderSizeInQuote);
    Assert.AreEqual(0.0001m, result.MinOrderSizeInBase);
  }

  [TestMethod]
  public async Task GetMarket_SecondCallSameMarket_ReusesCache_NoNewHttpRequest()
  {
    // Arrange
    var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, _btcMarketBody);

    var exchange = NewExchange(handler);

    // Act
    _ = await exchange.GetMarket(_credentials, new MarketReqDto("EUR", "BTC"));
    _ = await exchange.GetMarket(_credentials, new MarketReqDto("EUR", "BTC"));

    // Assert
    Assert.AreEqual(1, handler.RequestCount);
  }

  [TestMethod]
  public async Task GetMarket_DifferentMarket_TriggersSeparateFetch()
  {
    // Arrange — per-market caching, unlike a bulk fetch, means a cached BTC-EUR result does not
    // satisfy a lookup for a different market: each market is fetched (and cached) independently.
    var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, _btcMarketBody);

    var exchange = NewExchange(handler);

    // Act
    _ = await exchange.GetMarket(_credentials, new MarketReqDto("EUR", "BTC"));

    handler.SetResponse(HttpStatusCode.OK, _adaMarketBody);
    var ada = await exchange.GetMarket(_credentials, new MarketReqDto("EUR", "ADA"));

    // Assert
    Assert.AreEqual(2, handler.RequestCount);
    Assert.AreEqual("?market=ADA-EUR", handler.LastRequest!.RequestUri!.Query);
    Assert.IsNotNull(ada);
    Assert.AreEqual(31m, ada.MinOrderSizeInBase);
  }

  [TestMethod]
  public async Task GetMarket_MarketNotFound_ReturnsUnavailable_NotNull()
  {
    // Arrange — errorCode 205 is Bitvavo's "market not found" response.
    var handler = new FakeHttpMessageHandler(HttpStatusCode.NotFound, """{"errorCode":"205","error":"Market not found."}""");

    var exchange = NewExchange(handler);

    // Act
    var result = await exchange.GetMarket(_credentials, new MarketReqDto("EUR", "DOGE"));

    // Assert
    Assert.IsNotNull(result);
    Assert.AreEqual(MarketStatus.Unavailable, result.Status);
  }

  [TestMethod]
  public async Task GetMarket_FetchFails_ReturnsNull_NotCached_RetriesOnNextCall()
  {
    // Arrange
    var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, """{"errorCode":"999","error":"Unexpected."}""");

    var exchange = NewExchange(handler);

    // Act
    var first = await exchange.GetMarket(_credentials, new MarketReqDto("EUR", "BTC"));
    var second = await exchange.GetMarket(_credentials, new MarketReqDto("EUR", "BTC"));

    // Assert — a failed fetch isn't cached, so the second call retries rather than staying null
    // for the rest of the cache's TTL.
    Assert.IsNull(first);
    Assert.IsNull(second);
    Assert.AreEqual(2, handler.RequestCount);
  }
}
