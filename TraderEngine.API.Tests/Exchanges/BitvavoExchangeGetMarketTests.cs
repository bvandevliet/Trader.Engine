using System.Net;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TraderEngine.API.Exchanges;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Exchanges;

namespace TraderEngine.API.Tests.Exchanges;

/// <summary>
/// Covers <see cref="BitvavoExchange.GetMarket"/>, which is backed by an instance-level cache of
/// Bitvavo's unfiltered <c>GET /markets</c> response rather than a per-market
/// <c>GET /markets?market=X</c> call — a rebalance run that checks several markets' status and
/// several orders' minimum base-asset size previously cost one HTTP call each; this collapses
/// all of them into a single bulk call per <see cref="BitvavoExchange"/> instance (which is
/// DI-registered Scoped, so in practice one per rebalance run).
/// </summary>
[TestClass]
public class BitvavoExchangeGetMarketTests
{
  private static readonly ExchangeCredentials _credentials = new("key", "secret");

  private const string _bulkMarketsBody =
    """
    [
      {"market":"BTC-EUR","status":"trading","minOrderInQuoteAsset":"5","minOrderInBaseAsset":"0.0001"},
      {"market":"ADA-EUR","status":"trading","minOrderInQuoteAsset":"5","minOrderInBaseAsset":"31"}
    ]
    """;

  private static BitvavoExchange NewExchange(FakeHttpMessageHandler handler)
  {
    var httpClient = new HttpClient(handler) { BaseAddress = new("https://api.bitvavo.com/v2/") };

    return new BitvavoExchange(Substitute.For<ILogger<BitvavoExchange>>(), httpClient, new BitvavoWebSocketConnectionPool(Substitute.For<ILoggerFactory>(), Substitute.For<ILogger<BitvavoWebSocketConnectionPool>>(), new BitvavoRateLimitState()));
  }

  [TestMethod]
  public async Task GetMarket_FirstCall_RequestsUnfilteredMarketsEndpoint_NoQueryString()
  {
    // Arrange
    var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, _bulkMarketsBody);

    var exchange = NewExchange(handler);

    // Act
    var result = await exchange.GetMarket(_credentials, new MarketReqDto("EUR", "BTC"));

    // Assert — no "market=" filter: fetches every market in one call, not just this one.
    Assert.IsNotNull(handler.LastRequest);
    Assert.AreEqual(HttpMethod.Get, handler.LastRequest.Method);
    Assert.AreEqual("/v2/markets", handler.LastRequest.RequestUri!.AbsolutePath);
    Assert.AreEqual(string.Empty, handler.LastRequest.RequestUri!.Query);

    Assert.IsNotNull(result);
    Assert.AreEqual(MarketStatus.Trading, result.Status);
    Assert.AreEqual(5m, result.MinOrderSizeInQuote);
    Assert.AreEqual(0.0001m, result.MinOrderSizeInBase);
  }

  [TestMethod]
  public async Task GetMarket_SecondCallForDifferentMarket_ReusesCache_NoNewHttpRequest()
  {
    // Arrange
    var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, _bulkMarketsBody);

    var exchange = NewExchange(handler);

    // Act
    _ = await exchange.GetMarket(_credentials, new MarketReqDto("EUR", "BTC"));
    var ada = await exchange.GetMarket(_credentials, new MarketReqDto("EUR", "ADA"));

    // Assert — both markets came from the one bulk fetch triggered by the first call.
    Assert.AreEqual(1, handler.RequestCount);
    Assert.IsNotNull(ada);
    Assert.AreEqual(31m, ada.MinOrderSizeInBase);
  }

  [TestMethod]
  public async Task GetMarket_MarketAbsentFromBulkResponse_ReturnsUnavailable_NotNull()
  {
    // Arrange
    var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, _bulkMarketsBody);

    var exchange = NewExchange(handler);

    // Act
    var result = await exchange.GetMarket(_credentials, new MarketReqDto("EUR", "DOGE"));

    // Assert
    Assert.IsNotNull(result);
    Assert.AreEqual(MarketStatus.Unavailable, result.Status);
  }

  [TestMethod]
  public async Task GetMarket_BulkFetchFails_ReturnsNull_NotCached_RetriesOnNextCall()
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
