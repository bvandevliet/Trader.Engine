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
  private readonly IApiCredentialsRepository _apiCredentialsRepository;
  private readonly ITraderEngineApiClient _apiClient;
  private readonly string _exchangeName;

  public DashboardModel(
    UserManager<AppUser> userManager,
    IApiCredentialsRepository apiCredentialsRepository,
    ITraderEngineApiClient apiClient,
    IOptions<TraderEngineApiSettings> apiSettings)
    : base(userManager)
  {
    _apiCredentialsRepository = apiCredentialsRepository;
    _apiClient = apiClient;
    _exchangeName = apiSettings.Value.ExchangeName;
  }

  private async Task<ApiCredReqDto> GetCredentialsOrThrow(Guid userId)
  {
    var credentials = await _apiCredentialsRepository.GetApiCred(userId, _exchangeName);

    if (string.IsNullOrEmpty(credentials.ApiKey) || string.IsNullOrEmpty(credentials.ApiSecret))
      throw new ExchangeAuthenticationException("Configure your exchange API keys before the dashboard can show any figures.");

    return credentials;
  }

  /// <summary>
  /// Renders the page shell only — the exchange calls that populate it are slow enough (network
  /// round-trip to the exchange itself) that blocking the initial render on them would defeat the
  /// point of the loading overlay, so the client fetches <see cref="OnGetSummaryAsync"/> instead.
  /// Still checks stored credentials (a cheap DB lookup) so users without keys configured yet are
  /// redirected immediately rather than shown an empty dashboard that will only ever 401.
  /// </summary>
  public async Task<IActionResult> OnGetAsync()
  {
    var user = await GetCurrentUserAsync();

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
  /// Fetched once by the dashboard page on load to populate the balance summary table via AJAX.
  /// </summary>
  public async Task<IActionResult> OnGetSummaryAsync(CancellationToken ct)
  {
    var user = await GetCurrentUserAsync();

    try
    {
      var credentials = await GetCredentialsOrThrow(user.Id);

      var depositedTask = _apiClient.GetTotalDeposited(user, _exchangeName, credentials, ct);
      var withdrawnTask = _apiClient.GetTotalWithdrawn(user, _exchangeName, credentials, ct);
      var balanceTask = _apiClient.GetCurrentBalance(user, _exchangeName, credentials, ct);

      await Task.WhenAll(depositedTask, withdrawnTask, balanceTask);

      var balance = balanceTask.Result;
      var quoteSymbol = balance.QuoteSymbol;

      return new JsonResult(new
      {
        quoteSymbol,
        quoteCurrencySign = quoteSymbol switch
        {
          "EUR" => "€",
          "USD" => "$",
          _ => quoteSymbol,
        },
        totalDeposited = depositedTask.Result,
        totalWithdrawn = withdrawnTask.Result,
        currentBalance = balance,
      });
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

  /// <summary>
  /// Polled every few seconds by the dashboard page to refresh just the balance row, mirroring
  /// the old frontend's 5-second current-balance poll.
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
