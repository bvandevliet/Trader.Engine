using TraderEngine.API.Exchanges;
using TraderEngine.Common.Factories;

namespace TraderEngine.API.Factories;

/// <summary>
/// Exchange factory used to get the exchange by name.
/// </summary>
public class ExchangeFactory : INamedTypeFactory<IExchange>
{
  private readonly IServiceProvider _serviceProvider;
  private readonly IDictionary<string, Type> _exchangeTypes;

  public ExchangeFactory(
    IServiceProvider serviceProvider,
    IEnumerable<Type> exchangeTypes)
  {
    _serviceProvider = serviceProvider;
    _exchangeTypes = exchangeTypes.ToDictionary(GetExchangeName, x => x, StringComparer.OrdinalIgnoreCase);
  }

  public IExchange GetService(string name)
  {
    if (_exchangeTypes.TryGetValue(name, out var exchangeType)
      && _serviceProvider.GetService(exchangeType) is IExchange exchange)
    {
      return exchange;
    }

    throw new ArgumentException($"Exchange '{name}' not found.");
  }

  private static string GetExchangeName(Type exchangeType) =>
    exchangeType.Name.Replace("Exchange", string.Empty);
}
