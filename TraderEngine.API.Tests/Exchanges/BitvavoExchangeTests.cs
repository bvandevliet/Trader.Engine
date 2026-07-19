using Microsoft.Extensions.Logging;
using NSubstitute;
using TraderEngine.API.Exchanges;
using TraderEngine.Common.Enums;

namespace TraderEngine.API.Tests.Exchanges;

[TestClass()]
public class BitvavoExchangeTests
{
  [TestMethod()]
  public async Task NewOrderTest()
  {
    var logger = Substitute.For<ILogger<BitvavoExchange>>();

    var httpClient = new HttpClient
    {
      BaseAddress = new("https://api.bitvavo.com/v2/")
    };

    var bitvavo = new BitvavoExchange(logger, httpClient);

    var result = await bitvavo.NewOrder(new()
    {
      Market = new("EUR", "BTC"),
      Side = OrderSide.Buy,
      Type = OrderType.Market,
      AmountQuote = 100,
    });
  }
}