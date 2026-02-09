using Microsoft.Extensions.Logging;
using Moq;
using TraderEngine.API.Exchanges;
using TraderEngine.API.Tests.Helpers;
using TraderEngine.Common.Enums;

namespace TraderEngine.API.Tests.Exchanges;

[TestClass()]
public class BitvavoExchangeTests
{
  [TestMethod()]
  public async Task NewOrderTest()
  {
    var loggerMock = new Mock<ILogger<BitvavoExchange>>();

    var mapper = MapperHelper.CreateMapper();

    var httpClient = new HttpClient
    {
      BaseAddress = new("https://api.bitvavo.com/v2/")
    };

    var bitvavo = new BitvavoExchange(loggerMock.Object, mapper, httpClient);

    var result = await bitvavo.NewOrder(new()
    {
      Market = new("EUR", "BTC"),
      Side = OrderSide.Buy,
      Type = OrderType.Market,
      AmountQuote = 100,
    });
  }
}