using Microsoft.VisualStudio.TestTools.UnitTesting;
using TraderEngine.Common.DTOs.API.Request;

namespace TraderEngine.Common.Models.Tests;

[TestClass()]
public class BalanceTests
{
  private readonly string _quoteSymbol = "EUR";

  /// <inheritdoc/>
  private class BalanceWrapper : Balance
  {
    private bool _amountQuoteTotalReset = false;
    private bool _amountQuoteAvailableReset = false;

    /// <inheritdoc/>
    public BalanceWrapper(string quoteCurrency) : base(quoteCurrency)
    {
      OnAmountQuoteTotalReset += (oldValue, newValue) => _amountQuoteTotalReset = true;
      OnAmountQuoteAvailableReset += (oldValue, newValue) => _amountQuoteAvailableReset = true;
    }

    internal bool AmountQuoteTotalResetEventTriggered() => _amountQuoteTotalReset && !(_amountQuoteTotalReset = false);
    internal bool AmountQuoteAvailableResetEventTriggered() => _amountQuoteAvailableReset && !(_amountQuoteAvailableReset = false);
  }

  [TestMethod()]
  public void AddAllocation()
  {
    var balance = new BalanceWrapper(_quoteSymbol);

    var alloc0 = new Allocation(new MarketReqDto(_quoteSymbol, _quoteSymbol), 1, 10);
    var alloc1 = new Allocation(new MarketReqDto(_quoteSymbol, "BTC"), 0, 0);
    var alloc2 = new Allocation(new MarketReqDto(_quoteSymbol, "ETH"), 0, 0);

    balance.AddAllocation(alloc1);
    balance.AddAllocation(alloc2);

    // Test if events are raised as expected.
    Assert.IsTrue(balance.AmountQuoteTotalResetEventTriggered());
    Assert.IsFalse(balance.AmountQuoteAvailableResetEventTriggered());

    balance.AddAllocation(alloc0);

    // Test if events are raised as expected.
    Assert.IsTrue(balance.AmountQuoteTotalResetEventTriggered());
    Assert.IsTrue(balance.AmountQuoteAvailableResetEventTriggered());

    // Both allocations should be added.
    Assert.AreEqual(3, balance.Allocations.Count);
  }

  [TestMethod()]
  public void RemoveAllocation()
  {
    var balance = new BalanceWrapper(_quoteSymbol);

    var alloc0 = new Allocation(new MarketReqDto(_quoteSymbol, _quoteSymbol), 1, 10);
    var alloc1 = new Allocation(new MarketReqDto(_quoteSymbol, "BTC"), 0, 0);
    var alloc2 = new Allocation(new MarketReqDto(_quoteSymbol, "ETH"), 0, 0);

    balance.AddAllocation(alloc0);
    balance.AddAllocation(alloc1);
    balance.AddAllocation(alloc2);

    // Reset event states.
    balance.AmountQuoteTotalResetEventTriggered();
    balance.AmountQuoteAvailableResetEventTriggered();

    balance.RemoveAllocation("BTC");

    // Test if events are raised as expected.
    Assert.IsTrue(balance.AmountQuoteTotalResetEventTriggered());
    Assert.IsFalse(balance.AmountQuoteAvailableResetEventTriggered());

    balance.RemoveAllocation(_quoteSymbol);

    // Test if events are raised as expected.
    Assert.IsTrue(balance.AmountQuoteTotalResetEventTriggered());
    Assert.IsTrue(balance.AmountQuoteAvailableResetEventTriggered());

    // Allocation should be removed leaving one.
    Assert.AreEqual(1, balance.Allocations.Count);
  }

  [TestMethod()]
  public void UpdateAllocation_Price()
  {
    var balance = new BalanceWrapper(_quoteSymbol);

    var alloc = new Allocation(new MarketReqDto(_quoteSymbol, "BTC"), 0, 5);

    balance.AddAllocation(alloc);

    // Test amount quote value.
    Assert.AreEqual(0 * 5, balance.AmountQuoteTotal);

    // Reset event states.
    balance.AmountQuoteTotalResetEventTriggered();
    balance.AmountQuoteAvailableResetEventTriggered();

    alloc.Price = 5; // was 0;

    // Test if events are raised as expected.
    Assert.IsTrue(balance.AmountQuoteTotalResetEventTriggered());
    Assert.IsFalse(balance.AmountQuoteAvailableResetEventTriggered());

    // Test amount quote value.
    Assert.AreEqual(5 * 5, balance.AmountQuoteTotal);
  }

  [TestMethod()]
  public void UpdateAllocation_Amount()
  {
    var balance = new BalanceWrapper(_quoteSymbol);

    var alloc = new Allocation(new MarketReqDto(_quoteSymbol, "BTC"), 5, 0);

    balance.AddAllocation(alloc);

    // Test amount quote value.
    Assert.AreEqual(5 * 0, balance.AmountQuoteTotal);

    // Reset event states.
    balance.AmountQuoteTotalResetEventTriggered();
    balance.AmountQuoteAvailableResetEventTriggered();

    alloc.Amount = 5; // was 0;

    // Test if events are raised as expected.
    Assert.IsTrue(balance.AmountQuoteTotalResetEventTriggered());
    Assert.IsFalse(balance.AmountQuoteAvailableResetEventTriggered());

    // Test amount quote value.
    Assert.AreEqual(5 * 5, balance.AmountQuoteTotal);
  }

  [TestMethod()]
  public void UpdateAllocation_AmountQuote()
  {
    var balance = new BalanceWrapper(_quoteSymbol);

    var alloc = new Allocation(new MarketReqDto(_quoteSymbol, "BTC"), 5, 5);

    balance.AddAllocation(alloc);

    // Test amount quote value.
    Assert.AreEqual(5 * 5, balance.AmountQuoteTotal);

    // Reset event states.
    balance.AmountQuoteTotalResetEventTriggered();
    balance.AmountQuoteAvailableResetEventTriggered();

    alloc.AmountQuote = 20; // was 5 * 5;

    // Test if events are raised as expected.
    Assert.IsTrue(balance.AmountQuoteTotalResetEventTriggered());
    Assert.IsFalse(balance.AmountQuoteAvailableResetEventTriggered());

    // Test amount quote value.
    Assert.AreEqual(20, balance.AmountQuoteTotal);
  }
}