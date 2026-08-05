using Microsoft.Extensions.Logging.Abstractions;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Exchanges;
using TraderEngine.Common.Services;
using TraderEngine.Common.Tests.Exchanges;

namespace TraderEngine.Common.Tests.Services;

[TestClass]
public class RebalancingServiceTopRankingTests
{
  private static readonly IRebalancingService _service = new RebalancingService(NullLogger<RebalancingService>.Instance);

  private static readonly ExchangeCredentials _credentials = new("test-key", "test-secret");

  #region FetchMarketStatus Tests

  [TestMethod]
  public async Task FetchMarketStatus_UnknownStatus_FetchesFromExchange()
  {
    // Arrange
    var exchange = new ScriptedExchange();
    exchange.SetMarketStatus("BTC", MarketStatus.Trading);

    var absAlloc = new AbsAllocReqDto(new MarketReqDto("EUR", "BTC"), .5m);

    // Act
    var result = await _service.FetchMarketStatus(exchange, _credentials, absAlloc);

    // Assert
    Assert.AreEqual(MarketStatus.Trading, result.MarketStatus);
    CollectionAssert.AreEqual(new[] { "BTC" }, exchange.GetMarketCalls);
  }

  [TestMethod]
  public async Task FetchMarketStatus_AlreadyKnownStatus_DoesNotRefetch()
  {
    // Arrange
    var exchange = new ScriptedExchange();
    // Configured to Trading, but the absAlloc already carries a Halted status —
    // GetMarket must not be consulted, so this configuration must be ignored.
    exchange.SetMarketStatus("BTC", MarketStatus.Trading);

    var absAlloc = new AbsAllocReqDto(new MarketReqDto("EUR", "BTC"), .5m)
    {
      MarketStatus = MarketStatus.Halted,
    };

    // Act
    var result = await _service.FetchMarketStatus(exchange, _credentials, absAlloc);

    // Assert
    Assert.AreEqual(MarketStatus.Halted, result.MarketStatus);
    Assert.AreEqual(0, exchange.GetMarketCalls.Count);
  }

  [TestMethod]
  public async Task FetchMarketStatus_NoMarketDataFound_ReturnsUnknown()
  {
    // Arrange
    // No status configured for BTC — ScriptedExchange.GetMarket returns null, simulating an
    // exchange that has no data for this market at all.
    var exchange = new ScriptedExchange();

    var absAlloc = new AbsAllocReqDto(new MarketReqDto("EUR", "BTC"), .5m);

    // Act
    var result = await _service.FetchMarketStatus(exchange, _credentials, absAlloc);

    // Assert
    Assert.AreEqual(MarketStatus.Unknown, result.MarketStatus);
  }

  #endregion

  #region GetTopRankingAllocs Tests

  [TestMethod]
  public async Task GetTopRankingAllocs_StopsAtTopRankingCount()
  {
    // Arrange
    // Five candidates, all tradable, but only the first three should be kept — the collection
    // is expected to already be ordered by market cap, so ranking order must be preserved.
    var exchange = new ScriptedExchange();
    exchange.SetMarketStatus("BTC", MarketStatus.Trading);
    exchange.SetMarketStatus("ETH", MarketStatus.Trading);
    exchange.SetMarketStatus("ADA", MarketStatus.Trading);
    exchange.SetMarketStatus("SOL", MarketStatus.Trading);
    exchange.SetMarketStatus("XRP", MarketStatus.Trading);

    var absAllocs = new[]
    {
      new AbsAllocReqDto(new MarketReqDto("EUR", "BTC"), .3m),
      new AbsAllocReqDto(new MarketReqDto("EUR", "ETH"), .3m),
      new AbsAllocReqDto(new MarketReqDto("EUR", "ADA"), .2m),
      new AbsAllocReqDto(new MarketReqDto("EUR", "SOL"), .1m),
      new AbsAllocReqDto(new MarketReqDto("EUR", "XRP"), .1m),
    };

    // Act
    var result = await _service.GetTopRankingAllocs(exchange, _credentials, absAllocs, topRankingCount: 3);

    // Assert
    CollectionAssert.AreEqual(
      new[] { "BTC", "ETH", "ADA" },
      result.Select(alloc => alloc.Market.BaseSymbol).ToList());
  }

  [TestMethod]
  public async Task GetTopRankingAllocs_SkipsAssetsWithUnknownMarketStatus()
  {
    // Arrange
    // ETH has no market data (delisted/unavailable) — it must be skipped entirely and must not
    // consume a slot of topRankingCount, letting SOL fill the third slot instead.
    var exchange = new ScriptedExchange();
    exchange.SetMarketStatus("BTC", MarketStatus.Trading);
    // ETH intentionally left unconfigured -> MarketStatus.Unknown.
    exchange.SetMarketStatus("ADA", MarketStatus.Trading);
    exchange.SetMarketStatus("SOL", MarketStatus.Trading);

    var absAllocs = new[]
    {
      new AbsAllocReqDto(new MarketReqDto("EUR", "BTC"), .4m),
      new AbsAllocReqDto(new MarketReqDto("EUR", "ETH"), .3m),
      new AbsAllocReqDto(new MarketReqDto("EUR", "ADA"), .2m),
      new AbsAllocReqDto(new MarketReqDto("EUR", "SOL"), .1m),
    };

    // Act
    var result = await _service.GetTopRankingAllocs(exchange, _credentials, absAllocs, topRankingCount: 3);

    // Assert
    CollectionAssert.AreEqual(
      new[] { "BTC", "ADA", "SOL" },
      result.Select(alloc => alloc.Market.BaseSymbol).ToList());
  }

  [TestMethod]
  public async Task GetTopRankingAllocs_DoesNotRefetchStatus_WhenAlreadyKnown()
  {
    // Arrange
    // BTC already carries a known status — GetMarket must not be called for it, yet it must
    // still be included in the result and count toward topRankingCount.
    var exchange = new ScriptedExchange();
    exchange.SetMarketStatus("ETH", MarketStatus.Trading);

    var absAllocs = new[]
    {
      new AbsAllocReqDto(new MarketReqDto("EUR", "BTC"), .5m) { MarketStatus = MarketStatus.Trading },
      new AbsAllocReqDto(new MarketReqDto("EUR", "ETH"), .5m),
    };

    // Act
    var result = await _service.GetTopRankingAllocs(exchange, _credentials, absAllocs, topRankingCount: 10);

    // Assert
    CollectionAssert.AreEqual(new[] { "BTC", "ETH" }, result.Select(a => a.Market.BaseSymbol).ToList());
    CollectionAssert.DoesNotContain(exchange.GetMarketCalls, "BTC");
    CollectionAssert.Contains(exchange.GetMarketCalls, "ETH");
  }

  [TestMethod]
  public async Task GetTopRankingAllocs_EmptyInput_ReturnsEmpty()
  {
    // Arrange
    var exchange = new ScriptedExchange();

    // Act
    var result = await _service.GetTopRankingAllocs(exchange, _credentials, Array.Empty<AbsAllocReqDto>(), topRankingCount: 10);

    // Assert
    Assert.AreEqual(0, result.Count);
  }

  #endregion
}
