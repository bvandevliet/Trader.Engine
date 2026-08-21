using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TraderEngine.API.Exchanges;
using TraderEngine.Common.Exchanges;

namespace TraderEngine.API.Tests.Exchanges;

/// <summary>
/// Covers <see cref="BitvavoExchange.GetAsset"/>, which caches each asset's result individually
/// (via <see cref="IMemoryCache"/>, 10s TTL) — same pattern as <see cref="BitvavoExchange.GetMarket"/>:
/// public, account-agnostic data, so caching it doesn't risk leaking anything user-specific across
/// callers sharing the underlying <see cref="IMemoryCache"/> singleton.
/// </summary>
[TestClass]
public class BitvavoExchangeGetAssetTests
{
  private static readonly ExchangeCredentials _credentials = new("key", "secret");

  private const string _btcAssetBody =
    """
    {"symbol":"BTC","name":"Bitcoin","decimals":8,"depositFee":"0","depositConfirmations":10,"depositStatus":"OK","withdrawalFee":"0.0002","withdrawalMinAmount":"0.0002","withdrawalStatus":"OK"}
    """;

  private const string _ethAssetBody =
    """
    {"symbol":"ETH","name":"Ethereum","decimals":18,"depositFee":"0","depositConfirmations":64,"depositStatus":"OK","withdrawalFee":"0.005","withdrawalMinAmount":"0.005","withdrawalStatus":"OK"}
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
  public async Task GetAsset_FirstCall_RequestsAssetEndpoint_WithQueryString()
  {
    // Arrange
    var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, _btcAssetBody);

    var exchange = NewExchange(handler);

    // Act
    var result = await exchange.GetAsset(_credentials, "BTC");

    // Assert
    Assert.IsNotNull(handler.LastRequest);
    Assert.AreEqual(HttpMethod.Get, handler.LastRequest.Method);
    Assert.AreEqual("/v2/assets", handler.LastRequest.RequestUri!.AbsolutePath);
    Assert.AreEqual("?symbol=BTC", handler.LastRequest.RequestUri!.Query);

    Assert.IsNotNull(result);
    Assert.AreEqual("BTC", result.BaseSymbol);
  }

  [TestMethod]
  public async Task GetAsset_SecondCallSameSymbol_ReusesCache_NoNewHttpRequest()
  {
    // Arrange
    var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, _btcAssetBody);

    var exchange = NewExchange(handler);

    // Act
    _ = await exchange.GetAsset(_credentials, "BTC");
    _ = await exchange.GetAsset(_credentials, "BTC");

    // Assert
    Assert.AreEqual(1, handler.RequestCount);
  }

  [TestMethod]
  public async Task GetAsset_DifferentSymbol_TriggersSeparateFetch()
  {
    // Arrange — per-symbol caching means a cached BTC result does not satisfy a lookup for a
    // different asset: each symbol is fetched (and cached) independently.
    var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, _btcAssetBody);

    var exchange = NewExchange(handler);

    // Act
    _ = await exchange.GetAsset(_credentials, "BTC");

    handler.SetResponse(HttpStatusCode.OK, _ethAssetBody);
    var eth = await exchange.GetAsset(_credentials, "ETH");

    // Assert
    Assert.AreEqual(2, handler.RequestCount);
    Assert.AreEqual("?symbol=ETH", handler.LastRequest!.RequestUri!.Query);
    Assert.IsNotNull(eth);
    Assert.AreEqual("ETH", eth.BaseSymbol);
  }

  [TestMethod]
  public async Task GetAsset_FetchFails_ReturnsNull_NotCached_RetriesOnNextCall()
  {
    // Arrange
    var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, """{"errorCode":"999","error":"Unexpected."}""");

    var exchange = NewExchange(handler);

    // Act
    var first = await exchange.GetAsset(_credentials, "BTC");
    var second = await exchange.GetAsset(_credentials, "BTC");

    // Assert — a failed fetch isn't cached, so the second call retries rather than staying null
    // for the rest of the cache's TTL.
    Assert.IsNull(first);
    Assert.IsNull(second);
    Assert.AreEqual(2, handler.RequestCount);
  }
}
