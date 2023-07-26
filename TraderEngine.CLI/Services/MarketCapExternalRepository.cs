using AutoMapper;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using System.Net.Http.Json;
using TraderEngine.CLI.AppSettings;
using TraderEngine.CLI.DTOs;
using TraderEngine.Common.DTOs.Request;
using TraderEngine.Common.DTOs.Response;

namespace TraderEngine.CLI.Services;

internal class MarketCapExternalRepository : IMarketCapExternalRepository
{
  private readonly CoinMarketCapSettings _cmcSettings;
  private readonly IMapper _mapper;
  private readonly HttpClient _httpClient;

  public MarketCapExternalRepository(
    IOptions<CoinMarketCapSettings> cmcSettings,
    IMapper mapper,
    HttpClient httpClient)
  {
    _cmcSettings = cmcSettings.Value;
    _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    _httpClient = httpClient;

    _httpClient.BaseAddress = new("https://pro-api.coinmarketcap.com/v1/");

    _httpClient.DefaultRequestHeaders.Add(HeaderNames.Accept, "application/json");

    _httpClient.DefaultRequestHeaders.Add("X-CMC_PRO_API_KEY", _cmcSettings.API_KEY);
  }

  public Task<MarketCapData> GetMarketCap(MarketDto market)
  {
    throw new NotImplementedException();
  }

  public async Task<IEnumerable<MarketCapData>> ListLatest(string quoteSymbol, int count = 100)
  {
    CMCListLatestDto? listLatest = await _httpClient.GetFromJsonAsync<CMCListLatestDto>(
      $"cryptocurrency/listings/latest?sort=market_cap&limit={count}&convert={quoteSymbol}");

    return _mapper.Map<IEnumerable<MarketCapData>>(listLatest?.data);
  }
}