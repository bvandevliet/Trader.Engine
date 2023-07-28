using AutoMapper;
using System.Net.Http.Json;
using TraderEngine.CLI.DTOs;
using TraderEngine.Common.DTOs.Request;
using TraderEngine.Common.DTOs.Response;

namespace TraderEngine.CLI.Services;

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

  public Task<MarketCapData> GetMarketCap(MarketDto market)
  {
    throw new NotImplementedException();
  }

  public async Task<IEnumerable<MarketCapData>> ListLatest(string quoteSymbol, int count = 100)
  {
    CMCListLatestDto? listLatest = await _httpClient.GetFromJsonAsync<CMCListLatestDto>(
      $"cryptocurrency/listings/latest?sort=market_cap&limit={count}&convert={quoteSymbol}");

    return _mapper.Map<IEnumerable<MarketCapData>>(listLatest?.Data);
  }
}