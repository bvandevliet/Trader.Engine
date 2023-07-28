using Microsoft.VisualStudio.TestTools.UnitTesting;
using TraderEngine.API.Exchanges;
using TraderEngine.Common.DTOs.Request;
using TraderEngine.Common.Models;

namespace TraderEngine.API.Extensions.Tests;
[TestClass()]
public class TraderTests
{
  private readonly string _quoteSymbol = "EUR";

  private readonly MockExchange _exchangeService;

  private readonly List<AbsAssetAllocReqDto> _absAssetAlloc = new()
  {
    new(baseSymbol: "EUR", absAlloc: .05m),
    new(baseSymbol: "BTC", absAlloc: .40m),
    new(baseSymbol: "ETH", absAlloc: .30m),
    new(baseSymbol: "ADA", absAlloc: .25m),
    //                               100%
  };

  public TraderTests()
  {
    _exchangeService = new(_quoteSymbol, 5, .0015m, .0025m);
  }

  [TestMethod()]
  public async Task AllocQuoteDiffsTest()
  {
    Balance curBalance = await _exchangeService.GetBalance();

    var allocQuoteDiffs = Trader.GetAllocationQuoteDiffs(_absAssetAlloc, curBalance).ToList();

    Assert.AreEqual(5, allocQuoteDiffs.Count);

    Assert.AreEqual(-005, (double)Math.Round(allocQuoteDiffs[0].Value, 1));
    Assert.AreEqual(0040, (double)Math.Round(allocQuoteDiffs[1].Value, 1));
    Assert.AreEqual(0015, (double)Math.Round(allocQuoteDiffs[2].Value, 1));
    Assert.AreEqual(0225, (double)Math.Round(allocQuoteDiffs[3].Value, 1));
    Assert.AreEqual(-275, (double)Math.Round(allocQuoteDiffs[4].Value, 1));
  }

  [TestMethod()]
  public async Task RebalanceTest()
  {
    var rebalanceOrders = (await _exchangeService.Rebalance(_absAssetAlloc)).ToList();

    Assert.AreEqual(4, rebalanceOrders.Count);

    Assert.AreEqual(1.3875m, Math.Round(rebalanceOrders.Sum(result => result.FeePaid), 4));

    Assert.IsNull(rebalanceOrders[0].Amount);
    Assert.IsNull(rebalanceOrders[1].Amount);
    Assert.IsNotNull(rebalanceOrders[2].Amount); // expected to sell whole position
    Assert.IsNull(rebalanceOrders[3].Amount);

    Balance curBalance = await _exchangeService.GetBalance();

    var allocQuoteDiffs = Trader.GetAllocationQuoteDiffs(_absAssetAlloc, curBalance).ToList();

    Assert.AreEqual(5, allocQuoteDiffs.Count);

    Assert.AreEqual(0, (double)Math.Round(allocQuoteDiffs[0].Value));
    Assert.AreEqual(0, (double)Math.Round(allocQuoteDiffs[1].Value));
    Assert.AreEqual(0, (double)Math.Round(allocQuoteDiffs[2].Value));
    Assert.AreEqual(0, (double)Math.Round(allocQuoteDiffs[3].Value));
    Assert.AreEqual(0, (double)Math.Round(allocQuoteDiffs[4].Value));
  }
}