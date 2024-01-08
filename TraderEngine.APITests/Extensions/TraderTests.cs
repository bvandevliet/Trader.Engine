using Microsoft.VisualStudio.TestTools.UnitTesting;
using TraderEngine.API.Exchanges;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.Models;

namespace TraderEngine.API.Extensions.Tests;

[TestClass()]
public class TraderTests
{
  private readonly string _quoteSymbol = "EUR";

  private MockExchange _exchangeService = null!;

  private readonly List<AbsAllocReqDto> _absAssetAlloc = new()
  {
    new(market: new (quoteSymbol: "EUR", baseSymbol: "EUR"), absAlloc: .05m),
    new(market: new (quoteSymbol: "EUR", baseSymbol: "BTC"), absAlloc: .40m),
    new(market: new (quoteSymbol: "EUR", baseSymbol: "ETH"), absAlloc: .30m),
    new(market: new (quoteSymbol: "EUR", baseSymbol: "ADA"), absAlloc: .25m),
    //                                                                 100%
  };

  private readonly ConfigReqDto _configReqDto = new();

  [TestInitialize()]
  public void TestInit()
  {
    Balance curBalance = new(_quoteSymbol);

    decimal deposit = 1000;

    curBalance.TryAddAllocation(new(market: new MarketReqDto(_quoteSymbol, baseSymbol: "EUR"), price: 000001, amount: .05m * deposit));
    curBalance.TryAddAllocation(new(market: new MarketReqDto(_quoteSymbol, baseSymbol: "BTC"), price: 18_000, amount: .40m * deposit / 15_000));
    curBalance.TryAddAllocation(new(market: new MarketReqDto(_quoteSymbol, baseSymbol: "ETH"), price: 01_610, amount: .30m * deposit / 01_400));
    curBalance.TryAddAllocation(new(market: new MarketReqDto(_quoteSymbol, baseSymbol: "BNB"), price: 000306, amount: .25m * deposit / 000340));
    //                                                                                                                100%

    _exchangeService = new(_quoteSymbol, 5, .0015m, .0025m, curBalance);
  }

  [TestMethod()]
  public async Task RebalanceTest()
  {
    var rebalanceOrders = (await _exchangeService.Rebalance(_configReqDto, _absAssetAlloc)).ToList();

    Assert.AreEqual(4, rebalanceOrders.Count);

    Assert.AreEqual(1.3875m, Math.Round(rebalanceOrders.Sum(result => result.FeePaid), 4));

    Assert.IsNull(rebalanceOrders[0].Amount);
    Assert.IsNull(rebalanceOrders[1].Amount);
    Assert.IsNotNull(rebalanceOrders[2].Amount); // expected to sell whole position
    Assert.IsNull(rebalanceOrders[3].Amount);

    //Balance curBalance = await _exchangeService.GetBalance();
  }
}