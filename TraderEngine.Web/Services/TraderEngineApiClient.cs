using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Extensions;
using TraderEngine.Data.Entities;
using TraderEngine.Data.Services;

namespace TraderEngine.Web.Services;

public class TraderEngineApiClient : ITraderEngineApiClient
{
  private readonly HttpClient _http;
  private readonly IJwtTokenService _jwtTokenService;

  public TraderEngineApiClient(HttpClient http, IJwtTokenService jwtTokenService)
  {
    _http = http;
    _jwtTokenService = jwtTokenService;
  }

  private async Task<HttpResponseMessage> PostAuthenticated<TBody>(AppUser user, string requestUri, TBody body, CancellationToken ct)
  {
    var (token, _) = _jwtTokenService.GenerateToken(user);

    using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
    {
      Content = AppJsonSerializer.CreateContent(body),
      Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
    };

    var response = await _http.SendAsync(request, ct);

    // TraderEngine.API returns 401 specifically when the exchange itself rejected the supplied
    // credentials (see RebalanceController/AccountController/AllocationsController's
    // ExchangeErrCodeEnum.AuthenticationError handling) — surfaced as a specific exception so
    // callers can show an actionable message instead of a generic unhandled-exception page.
    if (response.StatusCode == HttpStatusCode.Unauthorized)
      throw new ExchangeAuthenticationException("The exchange rejected the stored API credentials.");

    // Any other non-success response (e.g. 404 "No recent market cap records found" while
    // MarketCapIngestionService is still catching up on a freshly-started stack) carries the
    // actual reason as a JSON-encoded string body — surfaced as-is instead of letting
    // EnsureSuccessStatusCode()'s generic message reach an unhandled-exception page.
    if (!response.IsSuccessStatusCode)
      throw new TraderEngineApiException(response.StatusCode, await ReadErrorReason(response, ct));

    return response;
  }

  private async Task<HttpResponseMessage> GetAuthenticated(AppUser user, string requestUri, CancellationToken ct)
  {
    var (token, _) = _jwtTokenService.GenerateToken(user);

    using var request = new HttpRequestMessage(HttpMethod.Get, requestUri)
    {
      Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
    };

    var response = await _http.SendAsync(request, ct);

    if (!response.IsSuccessStatusCode)
      throw new TraderEngineApiException(response.StatusCode, await ReadErrorReason(response, ct));

    return response;
  }

  private static async Task<string> ReadErrorReason(HttpResponseMessage response, CancellationToken ct)
  {
    var rawBody = await response.Content.ReadAsStringAsync(ct);

    if (string.IsNullOrWhiteSpace(rawBody))
      return response.ReasonPhrase ?? $"Request failed with status {(int)response.StatusCode}.";

    try
    {
      return AppJsonSerializer.Deserialize<string>(rawBody) ?? rawBody;
    }
    catch (JsonException)
    {
      return rawBody;
    }
  }

  public async Task<decimal> GetTotalDeposited(AppUser user, string exchangeName, ApiCredReqDto credentials, CancellationToken ct = default)
  {
    var response = await PostAuthenticated(user, $"api/account/totals/deposited/{exchangeName}", credentials, ct);

    return await response.Content.DeserializeAsync<decimal>(ct);
  }

  public async Task<decimal> GetTotalWithdrawn(AppUser user, string exchangeName, ApiCredReqDto credentials, CancellationToken ct = default)
  {
    var response = await PostAuthenticated(user, $"api/account/totals/withdrawn/{exchangeName}", credentials, ct);

    return await response.Content.DeserializeAsync<decimal>(ct);
  }

  public async Task<BalanceDto> GetCurrentBalance(AppUser user, string exchangeName, ApiCredReqDto credentials, CancellationToken ct = default)
  {
    var response = await PostAuthenticated(user, $"api/allocations/current/{exchangeName}", credentials, ct);

    return (await response.Content.DeserializeAsync<BalanceDto>(ct))!;
  }

  public async Task<SimulationDto> SimulateRebalance(AppUser user, string exchangeName, string source, SimulationReqDto request, CancellationToken ct = default)
  {
    var response = await PostAuthenticated(user, $"api/rebalance/simulate/{exchangeName}?source={Uri.EscapeDataString(source)}", request, ct);

    return (await response.Content.DeserializeAsync<SimulationDto>(ct))!;
  }

  public async Task<OrderDto[]> Rebalance(AppUser user, string exchangeName, string source, RebalanceReqDto request, CancellationToken ct = default)
  {
    var response = await PostAuthenticated(user, $"api/rebalance/{exchangeName}?source={Uri.EscapeDataString(source)}", request, ct);

    return (await response.Content.DeserializeAsync<OrderDto[]>(ct))!;
  }

  public async Task<Dictionary<string, string>> GetAssetNames(AppUser user, IEnumerable<string> baseSymbols, CancellationToken ct = default)
  {
    var query = string.Join('&', baseSymbols.Select(baseSymbol => $"baseSymbols={Uri.EscapeDataString(baseSymbol)}"));

    var response = await GetAuthenticated(user, $"api/allocations/names?{query}", ct);

    return (await response.Content.DeserializeAsync<Dictionary<string, string>>(ct))!;
  }
}
