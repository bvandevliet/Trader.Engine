using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Data.Entities;
using TraderEngine.Data.Repositories;
using TraderEngine.Web.AppSettings;
using TraderEngine.Web.Services;

namespace TraderEngine.Web.Pages;

public class RebalanceModel : TraderEnginePageModelBase
{
  private const string Source = "webapp";

  private readonly IConfigRepository _configRepository;
  private readonly IApiCredentialsRepository _apiCredentialsRepository;
  private readonly ITraderEngineApiClient _apiClient;
  private readonly string _exchangeName;

  public RebalanceModel(
    UserManager<AppUser> userManager,
    IConfigRepository configRepository,
    IApiCredentialsRepository apiCredentialsRepository,
    ITraderEngineApiClient apiClient,
    IOptions<TraderEngineApiSettings> apiSettings)
    : base(userManager)
  {
    _configRepository = configRepository;
    _apiCredentialsRepository = apiCredentialsRepository;
    _apiClient = apiClient;
    _exchangeName = apiSettings.Value.ExchangeName;
  }

  [BindProperty]
  public ConfigReqDto Config { get; set; } = null!;

  public string LastRebalanceDisplay => Config.LastRebalance?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Never";

  private async Task<ApiCredReqDto> GetCredentialsOrThrow(Guid userId)
  {
    var credentials = await _apiCredentialsRepository.GetApiCred(userId, _exchangeName);

    if (string.IsNullOrEmpty(credentials.ApiKey) || string.IsNullOrEmpty(credentials.ApiSecret))
      throw new ExchangeAuthenticationException("Configure your exchange API keys before you can simulate or run a rebalance.");

    return credentials;
  }

  public async Task<IActionResult> OnGetAsync()
  {
    var user = await GetCurrentUserAsync();

    Config = await _configRepository.GetConfig(user.Id);

    var credentials = await _apiCredentialsRepository.GetApiCred(user.Id, _exchangeName);

    if (string.IsNullOrEmpty(credentials.ApiKey) || string.IsNullOrEmpty(credentials.ApiSecret))
    {
      TempData["Error"] = "Configure your exchange API keys before you can simulate or run a rebalance.";

      return RedirectToPage("/ExchangeApiKeys");
    }

    // No simulation here — that call runs the full market-cap ranking + rebalance calculation
    // against the exchange, which is too slow to block the initial page render on. The client
    // fetches it asynchronously via OnPostSimulateAsync (see rebalance.ts) right after load, the
    // same call it already uses to resimulate on every input change.
    return Page();
  }

  public async Task<IActionResult> OnPostSaveAsync()
  {
    var user = await GetCurrentUserAsync();

    // The client re-fetches a simulation via OnPostSimulateAsync as soon as the page (re-)renders
    // (see rebalance.ts) — no need to simulate here too just to redisplay the form with errors.
    if (!ModelState.IsValid)
      return Page();

    // Preserve the last-rebalance timestamp and the advanced allocation config fields, which
    // this form never edits — only the RebalanceReqDto-mirroring fields below are user input here.
    var existing = await _configRepository.GetConfig(user.Id);
    Config.LastRebalance = existing.LastRebalance;
    Config.AltWeightingFactors = existing.AltWeightingFactors;
    Config.TagsToInclude = existing.TagsToInclude;
    Config.TagsToIgnore = existing.TagsToIgnore;

    await _configRepository.SaveConfig(user.Id, Config);

    TempData["Notice"] = "Configuration updated.";

    return RedirectToPage();
  }

  public async Task<IActionResult> OnPostSimulateAsync([FromBody] ConfigReqDto config, CancellationToken ct)
  {
    var user = await GetCurrentUserAsync();

    try
    {
      var credentials = await GetCredentialsOrThrow(user.Id);
      var simulation = await _apiClient.SimulateRebalance(user, _exchangeName, Source, new SimulationReqDto(credentials, config), ct);

      return StatusCode(StatusCodes.Status200OK, simulation);
    }
    catch (ExchangeAuthenticationException ex)
    {
      return StatusCode(StatusCodes.Status401Unauthorized, new { error = ex.Message });
    }
    catch (TraderEngineApiException ex)
    {
      return StatusCode((int)ex.StatusCode, new { error = ex.Message });
    }
  }

  public class RebalanceNowRequest
  {
    public ConfigReqDto Config { get; set; } = null!;

    public List<AbsAllocReqDto> NewAbsAllocs { get; set; } = [];
  }

  public async Task<IActionResult> OnPostRebalanceNowAsync([FromBody] RebalanceNowRequest request, CancellationToken ct)
  {
    var user = await GetCurrentUserAsync();

    try
    {
      var credentials = await GetCredentialsOrThrow(user.Id);

      var orders = await _apiClient.Rebalance(
        user, _exchangeName, Source, new RebalanceReqDto(credentials, request.Config, request.NewAbsAllocs), ct);

      request.Config.LastRebalance = DateTime.UtcNow;
      await _configRepository.SaveConfig(user.Id, request.Config);

      return StatusCode(StatusCodes.Status200OK, orders);
    }
    catch (ExchangeAuthenticationException ex)
    {
      return StatusCode(StatusCodes.Status401Unauthorized, new { error = ex.Message });
    }
    catch (TraderEngineApiException ex)
    {
      return StatusCode((int)ex.StatusCode, new { error = ex.Message });
    }
  }
}
