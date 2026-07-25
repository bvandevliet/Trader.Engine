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

  public decimal TotalDeposited { get; set; }

  public decimal TotalWithdrawn { get; set; }

  public BalanceDto CurrentBalance { get; set; } = null!;

  public string QuoteSymbol => CurrentBalance.QuoteSymbol;

  public string QuoteCurrencySign => QuoteSymbol switch
  {
    "EUR" => "€",
    "USD" => "$",
    _ => QuoteSymbol,
  };

  public decimal CumulativeValue => CurrentBalance.AmountQuoteTotal + TotalWithdrawn;

  public decimal TotalGain => CumulativeValue - TotalDeposited;

  public decimal TotalGainPercent => TotalDeposited == 0 ? 0 : 100 * (CumulativeValue / TotalDeposited - 1);

  private async Task<ApiCredReqDto> GetCredentialsOrThrow(Guid userId)
  {
    var credentials = await _apiCredentialsRepository.GetApiCred(userId, _exchangeName);

    if (string.IsNullOrEmpty(credentials.ApiKey) || string.IsNullOrEmpty(credentials.ApiSecret))
      throw new ExchangeAuthenticationException("Configure your exchange API keys before the dashboard can show any figures.");

    return credentials;
  }

  public async Task<IActionResult> OnGetAsync(CancellationToken ct)
  {
    var user = await GetCurrentUserAsync();

    try
    {
      var credentials = await GetCredentialsOrThrow(user.Id);

      var depositedTask = _apiClient.GetTotalDeposited(user, _exchangeName, credentials, ct);
      var withdrawnTask = _apiClient.GetTotalWithdrawn(user, _exchangeName, credentials, ct);
      var balanceTask = _apiClient.GetCurrentBalance(user, _exchangeName, credentials, ct);

      await Task.WhenAll(depositedTask, withdrawnTask, balanceTask);

      TotalDeposited = depositedTask.Result;
      TotalWithdrawn = withdrawnTask.Result;
      CurrentBalance = balanceTask.Result;

      return Page();
    }
    catch (ExchangeAuthenticationException ex)
    {
      TempData["Error"] = ex.Message;
      return RedirectToPage("/ExchangeApiKeys");
    }
    catch (TraderEngineApiException ex)
    {
      // Not a credentials problem — the dashboard has no useful figures to fall back to here, so
      // this at least avoids a raw unhandled-exception page while the underlying cause is fixed.
      return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
      {
        Title = "An error occurred while processing your request.",
        Detail = ex.Message,
        Status = StatusCodes.Status500InternalServerError,
      });
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
