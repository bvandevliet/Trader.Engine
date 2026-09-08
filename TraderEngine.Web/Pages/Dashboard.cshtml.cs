using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
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
  private readonly IDelegatedAccessResolver _delegatedAccessResolver;
  private readonly string _exchangeName;

  public DashboardModel(
    UserManager<AppUser> userManager,
    IConfigRepository configRepository,
    IApiCredentialsRepository apiCredentialsRepository,
    ITraderEngineApiClient apiClient,
    IDelegatedAccessResolver delegatedAccessResolver,
    IOptions<TraderEngineApiSettings> apiSettings)
    : base(userManager)
  {
    _configRepository = configRepository;
    _apiCredentialsRepository = apiCredentialsRepository;
    _apiClient = apiClient;
    _delegatedAccessResolver = delegatedAccessResolver;
    _exchangeName = apiSettings.Value.ExchangeName;
  }

  [BindProperty]
  public ConfigReqDto Config { get; set; } = null!;

  /// <summary>
  /// Null when viewing your own dashboard; otherwise the client whose portfolio you're currently
  /// acting on as their portfolio manager. Set by every handler below from the resolved <see
  /// cref="DelegatedAccessContext"/> so the view can render the "acting as" banner and every AJAX
  /// call can keep carrying the same <c>actingAsClientId</c> forward.
  /// </summary>
  public AppUser? ActingForClient { get; set; }

  public string LastRebalanceDisplay => Config.LastRebalance is { } lastRebalance
    ? DateTime.SpecifyKind(lastRebalance, DateTimeKind.Utc).ToString("yyyy-MM-dd HH:mm:ss") + " UTC"
    : "Never";

  // JS overwrites this with the viewer's local timezone (see localizeTimestamps in format.ts);
  // the server-rendered UTC text above is the fallback for clients with JS disabled.
  public string? LastRebalanceUtc => Config.LastRebalance is { } lastRebalance
    ? DateTime.SpecifyKind(lastRebalance, DateTimeKind.Utc).ToString("o")
    : null;

  /// <summary>
  /// Fetches CoinMarketCap display names for every asset appearing in either side of a
  /// simulation's portfolio table, for the dashboard's info tooltips. Never fails the caller: a
  /// names lookup is a nice-to-have, not something that should turn a working simulation into an
  /// error page if it happens to time out or the API rejects it.
  /// </summary>
  private async Task<Dictionary<string, string>> GetAssetNamesOrEmpty(AppUser user, SimulationDto simulation,
    CancellationToken ct)
  {
    var baseSymbols = simulation.CurBalance.Allocations
      .Concat(simulation.NewBalance.Allocations)
      .Select(alloc => alloc.Market.BaseSymbol)
      .Distinct();

    try
    {
      return await _apiClient.GetAssetNames(user, baseSymbols, ct);
    }
    catch (TraderEngineApiException)
    {
      return [];
    }
  }

  private async Task<ApiCredReqDto> GetCredentialsOrThrow(Guid userId)
  {
    var credentials = await _apiCredentialsRepository.GetApiCred(userId, _exchangeName);

    if (string.IsNullOrEmpty(credentials.ApiKey) || string.IsNullOrEmpty(credentials.ApiSecret))
      throw new ExchangeAuthenticationException(
        "Configure your exchange API keys before the dashboard can show any figures.");

    return credentials;
  }

  /// <summary>
  /// Shared by every AJAX handler below: resolves who's acting on whose account, then that
  /// account's exchange credentials. <see cref="DelegatedAccessContext.EffectiveUser"/>'s id is
  /// passed as-is to <see cref="ITraderEngineApiClient"/> calls' <c>actingAsClientId</c> argument
  /// even when not delegated — <see cref="Data.Services.JwtTokenService"/> already guards on
  /// "same id as the token subject" before embedding the claim, so re-deriving that same
  /// condition here would just duplicate it.
  /// </summary>
  private async Task<(DelegatedAccessContext Ctx, ApiCredReqDto Credentials)> ResolveAndAuthenticate(AppUser caller,
    Guid? actingAsClientId)
  {
    var ctx = await _delegatedAccessResolver.ResolveAsync(caller, actingAsClientId);
    var credentials = await GetCredentialsOrThrow(ctx.EffectiveUser.Id);

    return (ctx, credentials);
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
    catch (DelegationAccessDeniedException ex)
    {
      return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
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
  public async Task<IActionResult> OnGetAsync(Guid? actingAsClientId)
  {
    var caller = await GetCurrentUserAsync();

    DelegatedAccessContext ctx;

    try
    {
      ctx = await _delegatedAccessResolver.ResolveAsync(caller, actingAsClientId);
    }
    catch (DelegationAccessDeniedException ex)
    {
      TempData["Error"] = ex.Message;
      return RedirectToPage("/Delegation");
    }

    ActingForClient = ctx.IsDelegated ? ctx.EffectiveUser : null;
    Config = await _configRepository.GetConfig(ctx.EffectiveUser.Id);

    try
    {
      await GetCredentialsOrThrow(ctx.EffectiveUser.Id);

      return Page();
    }
    catch (ExchangeAuthenticationException ex)
    {
      // Redirecting to /ExchangeApiKeys would show the MANAGER's own key page, not fix anything
      // for a client that hasn't configured keys yet — that page is (correctly) always scoped to
      // the caller's own account, never a delegated target's.
      if (ctx.IsDelegated)
      {
        TempData["Error"] = $"{ctx.EffectiveUser.DisplayName} hasn't configured exchange API keys yet.";
        return RedirectToPage("/Delegation");
      }

      TempData["Error"] = ex.Message;
      return RedirectToPage("/ExchangeApiKeys");
    }
  }

  /// <summary>
  /// Takes the config over the wire rather than via the usual <see cref="Config"/> bind property
  /// — the form posts via fetch like every other handler on this page now, not a browser
  /// navigation, so there's no page re-render to fall back on for redisplaying validation errors.
  /// </summary>
  public async Task<IActionResult> OnPostSaveAsync([FromBody] ConfigReqDto config, Guid? actingAsClientId)
  {
    var caller = await GetCurrentUserAsync();

    DelegatedAccessContext ctx;

    try
    {
      ctx = await _delegatedAccessResolver.ResolveAsync(caller, actingAsClientId);
    }
    catch (DelegationAccessDeniedException ex)
    {
      return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
    }

    var validationResults = new List<ValidationResult>();

    if (!Validator.TryValidateObject(config, new ValidationContext(config), validationResults,
          validateAllProperties: true))
    {
      return StatusCode(StatusCodes.Status400BadRequest, new
      {
        error = string.Join(" ", validationResults.Select(result => result.ErrorMessage)),
      });
    }

    // Preserve the last-rebalance timestamp and the advanced allocation config fields, which
    // this form never edits — only the RebalanceReqDto-mirroring fields above are user input here.
    var existing = await _configRepository.GetConfig(ctx.EffectiveUser.Id);
    config.LastRebalance = existing.LastRebalance;
    config.WeightingOverrides = existing.WeightingOverrides;
    config.TagsToInclude = existing.TagsToInclude;
    config.TagsToIgnore = existing.TagsToIgnore;

    await _configRepository.SaveConfig(ctx.EffectiveUser.Id, config);

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
  ///
  /// The simulation leg is awaited separately from the deposited/withdrawn totals — it depends on
  /// market cap data (see TraderEngineApiClient.PostAuthenticated) that can still be catching up
  /// right after the stack starts, while the totals have no such dependency. A single
  /// Task.WhenAll across all three would let a failing simulation discard totals that had already
  /// succeeded, hiding them from the dashboard for no reason.
  /// </summary>
  public async Task<IActionResult> OnPostInitAsync([FromBody] ConfigReqDto config, Guid? actingAsClientId,
    CancellationToken ct)
  {
    var caller = await GetCurrentUserAsync();

    return await ExecuteExchangeCall(async () =>
    {
      var (ctx, credentials) = await ResolveAndAuthenticate(caller, actingAsClientId);

      var depositedTask =
        _apiClient.GetTotalDeposited(ctx.Caller, _exchangeName, credentials, ctx.EffectiveUser.Id, ct);
      var withdrawnTask =
        _apiClient.GetTotalWithdrawn(ctx.Caller, _exchangeName, credentials, ctx.EffectiveUser.Id, ct);
      var simulationTask = _apiClient.SimulateRebalance(ctx.Caller, _exchangeName, Source,
        new SimulationReqDto(credentials, config), ctx.EffectiveUser.Id, ct);

      await Task.WhenAll(depositedTask, withdrawnTask);

      SimulationDto? simulation = null;
      string? simulationError = null;
      Dictionary<string, string> assetNames = [];

      try
      {
        simulation = await simulationTask;
        assetNames = await GetAssetNamesOrEmpty(ctx.Caller, simulation, ct);
      }
      catch (TraderEngineApiException ex)
      {
        simulationError = ex.Message;
      }

      return new
      {
        totalDeposited = depositedTask.Result,
        totalWithdrawn = withdrawnTask.Result,
        simulation,
        simulationError,
        assetNames,
      };
    });
  }

  /// <summary>
  /// Re-run on every rebalance parameter change. Only the simulation, unlike <see
  /// cref="OnPostInitAsync"/> — the deposited/withdrawn totals it also fetches don't change based
  /// on rebalance config, so refetching them on every debounced keystroke would be wasted calls.
  /// </summary>
  public async Task<IActionResult> OnPostSimulateAsync([FromBody] ConfigReqDto config, Guid? actingAsClientId,
    CancellationToken ct)
  {
    var caller = await GetCurrentUserAsync();

    return await ExecuteExchangeCall(async () =>
    {
      var (ctx, credentials) = await ResolveAndAuthenticate(caller, actingAsClientId);

      var simulation = await _apiClient.SimulateRebalance(ctx.Caller, _exchangeName, Source,
        new SimulationReqDto(credentials, config), ctx.EffectiveUser.Id, ct);
      var assetNames = await GetAssetNamesOrEmpty(ctx.Caller, simulation, ct);

      return new
      {
        simulation,
        assetNames
      };
    });
  }

  public class RebalanceNowRequest
  {
    public ConfigReqDto Config { get; set; } = null!;

    public List<TargetAllocReqDto> TargetAllocs { get; set; } = [];
  }

  /// <summary>
  /// Fetches the post-trade balance itself and returns it alongside the executed orders, rather
  /// than making the client do a separate <see cref="OnGetCurrentBalanceAsync"/> round-trip right
  /// after this one just to refresh the two tables — same reasoning as <see cref="OnPostInitAsync"/>.
  /// </summary>
  public async Task<IActionResult> OnPostRebalanceNowAsync([FromBody] RebalanceNowRequest request,
    Guid? actingAsClientId, CancellationToken ct)
  {
    var caller = await GetCurrentUserAsync();

    return await ExecuteExchangeCall(async () =>
    {
      var (ctx, credentials) = await ResolveAndAuthenticate(caller, actingAsClientId);

      var orders = await _apiClient.Rebalance(
        ctx.Caller, _exchangeName, Source, new RebalanceReqDto(credentials, request.Config, request.TargetAllocs),
        ctx.EffectiveUser.Id, ct);

      // LastRebalance is persisted server-side by TraderEngine.API's RebalanceController itself,
      // right after the rebalance actually runs — not here, since this line would never run (and
      // the timestamp would silently go stale) if the client disconnects before the API response
      // makes it back, even though the rebalance already executed for real.

      // Only fetched after the trades above have settled — this needs the post-trade balance, so
      // it can't run in parallel with placing the orders the way OnPostInitAsync's calls can.
      var currentBalance =
        await _apiClient.GetCurrentBalance(ctx.Caller, _exchangeName, credentials, ctx.EffectiveUser.Id, ct);

      return new
      {
        orders,
        currentBalance
      };
    });
  }

  /// <summary>
  /// Polled every few seconds by the dashboard page to refresh just the balance summary row,
  /// mirroring the old frontend's 5-second current-balance poll.
  /// </summary>
  public async Task<IActionResult> OnGetCurrentBalanceAsync(Guid? actingAsClientId, CancellationToken ct)
  {
    var caller = await GetCurrentUserAsync();

    try
    {
      var (ctx, credentials) = await ResolveAndAuthenticate(caller, actingAsClientId);
      var balance =
        await _apiClient.GetCurrentBalance(ctx.Caller, _exchangeName, credentials, ctx.EffectiveUser.Id, ct);

      return new JsonResult(balance);
    }
    catch (ExchangeAuthenticationException)
    {
      return StatusCode(StatusCodes.Status401Unauthorized);
    }
    catch (DelegationAccessDeniedException)
    {
      return StatusCode(StatusCodes.Status403Forbidden);
    }
    catch (TraderEngineApiException ex)
    {
      return StatusCode((int)ex.StatusCode);
    }
  }
}