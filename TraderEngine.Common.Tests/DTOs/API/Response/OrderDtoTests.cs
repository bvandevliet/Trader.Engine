using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Enums;

namespace TraderEngine.Common.Tests.DTOs.API.Response;

/// <summary>
/// Covers <see cref="OrderDto.ToString"/>, which feeds directly into
/// <c>EmailNotificationService</c>'s failure-notification email (every order in the "orders
/// attempted" list is rendered via this override) — the exact text a user sees when trying to
/// understand why a rebalance failed.
/// </summary>
[TestClass]
public class OrderDtoTests
{
  private static readonly MarketReqDto _market = new("EUR", "ADA");

  [TestMethod]
  public void ToString_FailedOrder_StillShowsTheRequestedSize()
  {
    // A failed order has zero AmountFilled/AmountQuoteFilled by definition — without also showing
    // what was actually requested, the rendered line would read "filled 0 (0 EUR)" with no way to
    // tell how large the attempt was, which is exactly what prompted this fix.
    var order = new OrderDto
    {
      Market = _market,
      Side = OrderSide.Buy,
      Type = OrderType.Limit,
      Price = 0.19187m,
      Amount = 152.41927m,
      Status = OrderStatus.Failed,
      AmountFilled = 0,
      AmountQuoteFilled = 0,
    };

    var text = order.ToString();

    StringAssert.Contains(text, "requested 152.41927 ADA @ 0.19187");
    StringAssert.Contains(text, "Failed");
    StringAssert.Contains(text, "filled 0 (0 EUR)");
  }

  [TestMethod]
  public void ToString_MarketOrderRequestedByAmountQuote_ShowsQuoteAmountNotBaseAmount()
  {
    var order = new OrderDto
    {
      Market = _market,
      Side = OrderSide.Buy,
      Type = OrderType.Market,
      AmountQuote = 50m,
      Status = OrderStatus.Filled,
      AmountFilled = 250m,
      AmountQuoteFilled = 50m,
    };

    var text = order.ToString();

    StringAssert.Contains(text, "requested 50 EUR");
  }

  [TestMethod]
  public void ToString_Superseded_StillTagsSupersededAlongsideRequestedSize()
  {
    var order = new OrderDto
    {
      Market = _market,
      Side = OrderSide.Sell,
      Type = OrderType.Limit,
      Price = 1.28m,
      Amount = 10m,
      Status = OrderStatus.Canceled,
      IsSuperseded = true,
      AmountFilled = 4m,
      AmountQuoteFilled = 5.12m,
    };

    var text = order.ToString();

    StringAssert.Contains(text, "(superseded)");
    StringAssert.Contains(text, "requested 10 ADA @ 1.28");
  }
}
