using TraderEngine.API.DTOs.Bitvavo.Response;
using TraderEngine.Common.DTOs.Request;
using TraderEngine.Common.DTOs.Response;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Models;

namespace TraderEngine.API.Exchanges;

public class BitvavoExchange : IExchange
{
  private readonly HttpClient _httpClient;

  public string QuoteSymbol { get; } = "EUR";

  public decimal MinimumOrderSize { get; } = 5;

  public decimal MakerFee { get; } = .0015m;

  public decimal TakerFee { get; } = .0025m;

  public BitvavoExchange(HttpClient httpClient)
  {
    _httpClient = httpClient;
  }

  public async Task<Balance> GetBalance()
  {
    var balance = new Balance(QuoteSymbol);

    var result = await _httpClient.GetFromJsonAsync<List<BitvavoAllocationDto>>("balance");

    if (null == result)
    {
      // TODO: THROW !!
      return balance;
    }

    foreach (BitvavoAllocationDto allocationDto in result)
    {
      var market = new MarketReqDto(QuoteSymbol, allocationDto.Symbol);

      decimal price = allocationDto.Symbol == QuoteSymbol ? 1 : await GetPrice(market);

      decimal available = decimal.Parse(allocationDto.Available) + decimal.Parse(allocationDto.InOrder);

      var allocation = new Allocation(market, price, available);

      balance.AddAllocation(allocation);
    }

    return balance;
  }

  public Task<object> DepositHistory()
  {
    throw new NotImplementedException();
  }

  public Task<object> WithdrawHistory()
  {
    throw new NotImplementedException();
  }

  public Task<object> GetCandles(MarketReqDto market, CandleInterval interval, int limit)
  {
    throw new NotImplementedException();
  }

  public Task<bool> IsTradable(MarketReqDto market)
  {
    return Task.FromResult(true);
  }

  public async Task<decimal> GetPrice(MarketReqDto market)
  {
    var price = await _httpClient.GetFromJsonAsync<BitvavoTickerPriceDto>(
      $"ticker/price?market={market.BaseSymbol}-{market.QuoteSymbol}");

    // TODO: Throw if GET fails.
    return null == price ? 0 : decimal.Parse(price.Price);
  }

  public Task<OrderDto> NewOrder(OrderReqDto order)
  {
    throw new NotImplementedException();
  }

  public Task<OrderDto?> GetOrder(string orderId, MarketReqDto? market = null)
  {
    throw new NotImplementedException();
  }

  public Task<OrderDto?> CancelOrder(string orderId, MarketReqDto? market = null)
  {
    throw new NotImplementedException();
  }

  public Task<IEnumerable<OrderDto>> GetOpenOrders(MarketReqDto? market = null)
  {
    throw new NotImplementedException();
  }

  public Task<IEnumerable<OrderDto>> CancelAllOpenOrders(MarketReqDto? market = null)
  {
    return Task.FromResult((IEnumerable<OrderDto>)new List<OrderReqDto>());
  }

  public Task<IEnumerable<OrderDto>> SellAllPositions(string? asset = null)
  {
    throw new NotImplementedException();
  }
}