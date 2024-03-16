using AutoMapper;
using System.Net.Http.Json;
using TraderEngine.CLI.DTOs.CMC;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;

namespace TraderEngine.CLI.Repositories;

internal class MarketCapExternalRepository : IMarketCapExternalRepository
{
  private readonly IMapper _mapper;
  private readonly HttpClient _httpClient;

  public MarketCapExternalRepository(
    IMapper mapper,
    HttpClient httpClient)
  {
    _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
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

    return _mapper.Map<IEnumerable<MarketCapDataDto>>(listLatest?.Data);
  }
}