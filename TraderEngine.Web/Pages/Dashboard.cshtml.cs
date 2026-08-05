using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Data.Entities;
using TraderEngine.Data.Repositories;
using TraderEngine.Web.AppSettings;
using TraderEngine.Web.Services;

namespace TraderEngine.Web.Pages;

public class DashboardModel : TraderEnginePageModelBase
{
  private const string Source = "webapp";

  private readonly IConfigRepository _configRepository;
  private readonly IApiCredentialsRepository _apiCredentialsRepository;
  private readonly ITraderEngineApiClient _apiClient;
  private readonly string _exchangeName;

  public DashboardModel(
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

  public string LastRebalanceDisplay => Config.LastRebalance is { } lastRebalance
    ? DateTime.SpecifyKind(lastRebalance, DateTimeKind.Utc).ToString("yyyy-MM-dd HH:mm:ss") + " UTC"
    : "Never";

  // JS overwrites this with the viewer's local timezone (see localizeTimestamps in format.ts);
  // the server-rendered UTC text above is the fallback for clients with JS disabled.
  public string? LastRebalanceUtc => Config.LastRebalance is { } lastRebalance
    ? DateTime.SpecifyKind(lastRebalance, DateTimeKind.Utc).ToString("o")
    : null;

  private async Task<ApiCredReqDto> GetCredentialsOrThrow(Guid userId)
  {
    var credentials = await _apiCredentialsRepository.GetApiCred(userId, _exchangeName);

    if (string.IsNullOrEmpty(credentials.ApiKey) || string.IsNullOrEmpty(credentials.ApiSecret))
      throw new ExchangeAuthenticationException("Configure your exchange API keys before the dashboard can show any figures.");

    return credentials;
  }

  /// <summary>
  /// Shared by every handler below that talks to the exchange via <see cref="ITraderEngineApiClient"/>
  /// — translates the two exceptions that boundary can throw into the same JSON error shape the
  /// client's <c>postJson</c> helper expects, so callers only need to describe the call itself.
  /// </summary>
  private async Task<IActionResult> ExecuteExchangeCall<T>(Func<Task<T>> action)
    where T : notnull
  {
    try
    {
      return StatusCode(StatusCodes.Status200OK, await action());
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

  /// <summary>
  /// Renders the page shell only — the exchange calls that populate it are slow enough (network
  /// round-trips to the exchange itself) that blocking the initial render on them would defeat
  /// the point of the loading overlays, so the client fetches <see cref="OnPostInitAsync"/>
  /// instead. Still checks stored credentials (a cheap DB lookup) so users without keys configured
  /// yet are redirected immediately rather than shown an empty dashboard that will only ever 401.
  /// </summary>
  public async Task<IActionResult> OnGetAsync()
  {
    var user = await GetCurrentUserAsync();

    Config = await _configRepository.GetConfig(user.Id);

    try
    {
      await GetCredentialsOrThrow(user.Id);

      return Page();
    }
    catch (ExchangeAuthenticationException ex)
    {
      TempData["Error"] = ex.Message;
      return RedirectToPage("/ExchangeApiKeys");
    }
  }

  /// <summary>
  /// Takes the config over the wire rather than via the usual <see cref="Config"/> bind property
  /// — the form posts via fetch like every other handler on this page now, not a browser
  /// navigation, so there's no page re-render to fall back on for redisplaying validation errors.
  /// </summary>
  public async Task<IActionResult> OnPostSaveAsync([FromBody] ConfigReqDto config)
  {
    var user = await GetCurrentUserAsync();

    var validationResults = new List<ValidationResult>();

    if (!Validator.TryValidateObject(config, new ValidationContext(config), validationResults, validateAllProperties: true))
    {
      return StatusCode(StatusCodes.Status400BadRequest, new
      {
        error = string.Join(" ", validationResults.Select(result => result.ErrorMessage)),
      });
    }

    // Preserve the last-rebalance timestamp and the advanced allocation config fields, which
    // this form never edits — only the RebalanceReqDto-mirroring fields above are user input here.
    var existing = await _configRepository.GetConfig(user.Id);
    config.LastRebalance = existing.LastRebalance;
    config.AltWeightingFactors = existing.AltWeightingFactors;
    config.TagsToInclude = existing.TagsToInclude;
    config.TagsToIgnore = existing.TagsToIgnore;

    await _configRepository.SaveConfig(user.Id, config);

    return StatusCode(StatusCodes.Status200OK);
  }

  /// <summary>
  /// Fetched once by the dashboard page on load to populate both the balance summary table and
  /// the rebalance portfolio table via a single AJAX round-trip. Deliberately does not call
  /// <see cref="ITraderEngineApiClient.GetCurrentBalance"/> on top of <see
  /// cref="ITraderEngineApiClient.SimulateRebalance"/> — the simulation response already carries
  /// the current balance (<c>curBalance</c>), and fetching it twice would just double the wait on
  /// the slowest call this page makes. Deposited/withdrawn totals aren't part of the simulation
  /// response, so those still need their own calls, run in parallel with the simulation.
  /// </summary>
  public async Task<IActionResult> OnPostInitAsync([FromBody] ConfigReqDto config, CancellationToken ct)
  {
    var user = await GetCurrentUserAsync();

    return await ExecuteExchangeCall(async () =>
    {
      var credentials = await GetCredentialsOrThrow(user.Id);

      var depositedTask = _apiClient.GetTotalDeposited(user, _exchangeName, credentials, ct);
      var withdrawnTask = _apiClient.GetTotalWithdrawn(user, _exchangeName, credentials, ct);
      var simulationTask = _apiClient.SimulateRebalance(user, _exchangeName, Source, new SimulationReqDto(credentials, config), ct);

      await Task.WhenAll(depositedTask, withdrawnTask, simulationTask);

      return new
      {
        totalDeposited = depositedTask.Result,
        totalWithdrawn = withdrawnTask.Result,
        simulation = simulationTask.Result,
      };
    });
  }

  /// <summary>
  /// Re-run on every rebalance parameter change. Only the simulation, unlike <see
  /// cref="OnPostInitAsync"/> — the deposited/withdrawn totals it also fetches don't change based
  /// on rebalance config, so refetching them on every debounced keystroke would be wasted calls.
  /// </summary>
  public async Task<IActionResult> OnPostSimulateAsync([FromBody] ConfigReqDto config, CancellationToken ct)
  {
    var user = await GetCurrentUserAsync();

    return await ExecuteExchangeCall(async () =>
    {
      var credentials = await GetCredentialsOrThrow(user.Id);

      return await _apiClient.SimulateRebalance(user, _exchangeName, Source, new SimulationReqDto(credentials, config), ct);
    });
  }

  public class RebalanceNowRequest
  {
    public ConfigReqDto Config { get; set; } = null!;

    public List<AbsAllocReqDto> NewAbsAllocs { get; set; } = [];
  }

  /// <summary>
  /// Fetches the post-trade balance itself and returns it alongside the executed orders, rather
  /// than making the client do a separate <see cref="OnGetCurrentBalanceAsync"/> round-trip right
  /// after this one just to refresh the two tables — same reasoning as <see cref="OnPostInitAsync"/>.
  /// </summary>
  public async Task<IActionResult> OnPostRebalanceNowAsync([FromBody] RebalanceNowRequest request, CancellationToken ct)
  {
    var user = await GetCurrentUserAsync();

    return await ExecuteExchangeCall(async () =>
    {
      var credentials = await GetCredentialsOrThrow(user.Id);

      var orders = await _apiClient.Rebalance(
        user, _exchangeName, Source, new RebalanceReqDto(credentials, request.Config, request.NewAbsAllocs), ct);

      request.Config.LastRebalance = DateTime.UtcNow;
      await _configRepository.SaveConfig(user.Id, request.Config);

      // Only fetched after the trades above have settled — this needs the post-trade balance, so
      // it can't run in parallel with placing the orders the way OnPostInitAsync's calls can.
      var currentBalance = await _apiClient.GetCurrentBalance(user, _exchangeName, credentials, ct);

      return new { orders, currentBalance };
    });
  }

  /// <summary>
  /// Polled every few seconds by the dashboard page to refresh just the balance summary row,
  /// mirroring the old frontend's 5-second current-balance poll.
  /// </summary>
  public async Task<IActionResult> OnGetCurrentBalanceAsync(CancellationToken ct)
  {
    var user = await GetCurrentUserAsync();

    try
    {
      var credentials = await GetCredentialsOrThrow(user.Id);
      var balance = await _apiClient.GetCurrentBalance(user, _exchangeName, credentials, ct);

      return new JsonResult(balance);
    }
    catch (ExchangeAuthenticationException)
    {
      return StatusCode(StatusCodes.Status401Unauthorized);
    }
    catch (TraderEngineApiException ex)
    {
      return StatusCode((int)ex.StatusCode);
    }
  }
}
