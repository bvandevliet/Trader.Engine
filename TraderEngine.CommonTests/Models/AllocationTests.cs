using Microsoft.VisualStudio.TestTools.UnitTesting;
using TraderEngine.Common.DTOs.Request;

namespace TraderEngine.Common.Models.Tests;

[TestClass()]
public class AllocationTests
{
  private readonly string _quoteSymbol = "EUR";
  private readonly string _baseSymbol = "BTC";

  private class AllocationWrapper : Allocation
  {
    private bool _priceUpdate = false;
    private bool _amountUpdate = false;
    private bool _amountQuoteUpdate = false;

    public AllocationWrapper(
      MarketReqDto market,
      decimal price,
      decimal? amount = null)
      : base(market, price, amount)
    {
      PriceUpdated += (oldValue, newValue) => _priceUpdate = true;
      AmountUpdated += (oldValue, newValue) => _amountUpdate = true;
      AmountQuoteUpdated += (oldValue, newValue) => _amountQuoteUpdate = true;
    }

    internal bool PriceUpdateEventTriggered() => _priceUpdate && !(_priceUpdate = false);
    internal bool AmountUpdateEventTriggered() => _amountUpdate && !(_amountUpdate = false);
    internal bool AmountQuoteUpdateEventTriggered() => _amountQuoteUpdate && !(_amountQuoteUpdate = false);
  }

  [TestMethod()]
  public void Initialize()
  {
    decimal price = 15;
    decimal amount = 25;

    // Create instance.
    var alloc = new AllocationWrapper(new MarketReqDto(_quoteSymbol, _baseSymbol), price, amount);

    // Test if quote amounts are correct.
    Assert.AreEqual(amount * price, alloc.AmountQuote);
  }

  [TestMethod()]
  public void UpdatePrice()
  {
    decimal price = 15;
    decimal amount = 25;

    var alloc = new AllocationWrapper(new MarketReqDto(_quoteSymbol, _baseSymbol), price, amount);

    price = alloc.Price = 10; // was 15

    // Test if events are raised (or not) as expected.
    Assert.IsTrue(alloc.PriceUpdateEventTriggered());
    Assert.IsFalse(alloc.AmountUpdateEventTriggered());
    Assert.IsFalse(alloc.AmountQuoteUpdateEventTriggered());

    // Test if quote amounts are correct.
    Assert.AreEqual(amount * price, alloc.AmountQuote);
  }

  [TestMethod()]
  public void IncreaseAmount()
  {
    decimal price = 15;
    decimal amount = 25;

    var alloc = new AllocationWrapper(new MarketReqDto(_quoteSymbol, _baseSymbol), price, amount);

    amount = alloc.Amount = 30; // was 25

    // Test if event is not raised.
    Assert.IsFalse(alloc.PriceUpdateEventTriggered());
    Assert.IsFalse(alloc.AmountQuoteUpdateEventTriggered());

    // Test if event is raised.
    Assert.IsTrue(alloc.AmountUpdateEventTriggered());

    // Test if increased accordingly.
    Assert.AreEqual(amount, alloc.Amount);

    // Test if quote amounts are correct.
    Assert.AreEqual(amount * price, alloc.AmountQuote);
  }

  [TestMethod()]
  public void DecreaseAmount()
  {
    decimal price = 15;
    decimal amount = 25;

    var alloc = new AllocationWrapper(new MarketReqDto(_quoteSymbol, _baseSymbol), price, amount);

    amount = alloc.Amount = 20; // was 25

    // Test if event is not raised.
    Assert.IsFalse(alloc.PriceUpdateEventTriggered());
    Assert.IsFalse(alloc.AmountQuoteUpdateEventTriggered());

    // Test if event is raised.
    Assert.IsTrue(alloc.AmountUpdateEventTriggered());

    // Test if decreased accordingly.
    Assert.AreEqual(amount, alloc.Amount);

    // Test if quote amounts are correct.
    Assert.AreEqual(amount * price, alloc.AmountQuote);
  }

  [TestMethod()]
  public void IncreaseAmountQuote()
  {
    decimal price = 15;
    decimal amount = 25;
    decimal amountQuote = amount * price;

    var alloc = new AllocationWrapper(new MarketReqDto(_quoteSymbol, _baseSymbol), price, amount);

    alloc.AmountQuote = amountQuote += 5;
    amount = amountQuote / price;

    // Test if event is not raised.
    Assert.IsFalse(alloc.PriceUpdateEventTriggered());
    Assert.IsFalse(alloc.AmountUpdateEventTriggered());

    // Test if event is raised.
    Assert.IsTrue(alloc.AmountQuoteUpdateEventTriggered());

    // Test if increased accordingly.
    Assert.AreEqual(amount, alloc.Amount);

    // Test if quote amounts are correct.
    Assert.AreEqual(amountQuote, alloc.AmountQuote);
  }

  [TestMethod()]
  public void DecreaseAmountQuote()
  {
    decimal price = 15;
    decimal amount = 25;

    var alloc = new AllocationWrapper(new MarketReqDto(_quoteSymbol, _baseSymbol), price, amount);

    decimal amountQuote = alloc.AmountQuote = amount = 0;

    // Test if event is not raised.
    Assert.IsFalse(alloc.PriceUpdateEventTriggered());
    Assert.IsFalse(alloc.AmountUpdateEventTriggered());

    // Test if event is raised.
    Assert.IsTrue(alloc.AmountQuoteUpdateEventTriggered());

    // Test if decreased accordingly.
    Assert.AreEqual(amount, alloc.Amount);

    // Test if quote amounts are correct.
    Assert.AreEqual(amountQuote, alloc.AmountQuote);
  }
}