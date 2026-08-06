using Microsoft.Extensions.Logging;
using NSubstitute;
using TraderEngine.API.Exchanges;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Exchanges;

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

    var wsPool = new BitvavoWebSocketConnectionPool(Substitute.For<ILoggerFactory>());

    var bitvavo = new BitvavoExchange(logger, httpClient, wsPool);

    var credentials = new ExchangeCredentials("key", "secret");

    var result = await bitvavo.NewOrder(credentials, new()
    {
      Market = new("EUR", "BTC"),
      Side = OrderSide.Buy,
      Type = OrderType.Market,
      AmountQuote = 100,
    });
  }
}