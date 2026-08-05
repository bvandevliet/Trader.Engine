using TraderEngine.API.DTOs.CMC;
using TraderEngine.API.Mappers;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;

namespace TraderEngine.API.Repositories;

internal class MarketCapExternalRepository : IMarketCapExternalRepository
{
  private readonly HttpClient _httpClient;

  public MarketCapExternalRepository(
    HttpClient httpClient)
  {
    _httpClient = httpClient;
  }

  public Task<MarketCapDataDto> GetMarketCap(MarketReqDto market)
  {
    throw new NotImplementedException();
  }

  public async Task<IEnumerable<MarketCapDataDto>> ListLatest(string quoteSymbol)
  {
    var listLatest = await _httpClient.GetFromJsonAsync<CMCListLatestDto>(
      $"cryptocurrency/listings/latest?sort=market_cap&limit=150&convert={quoteSymbol}");

    return CliMapper.MapCMCAssets(listLatest?.Data ?? []);
  }
}