using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Data.Entities;
using TraderEngine.Data.Repositories;
using TraderEngine.Web.AppSettings;

namespace TraderEngine.Web.Pages;

/// <summary>
/// Exchange API key management. Unlike the old WordPress block (which redisplayed the decrypted
/// key/secret on every page load), this form is write-only — the stored credentials are never
/// read back into the browser once saved, only ever overwritten. That's a deliberate hardening,
/// not a faithfulness gap: the plaintext-in-browser-history/autofill risk the old block carried
/// was flagged as worth fixing during the migration, not preserved as-is.
/// </summary>
public class ExchangeApiKeysModel : TraderEnginePageModelBase
{
  private readonly IApiCredentialsRepository _apiCredentialsRepository;
  private readonly IHttpClientFactory _httpClientFactory;
  private readonly string _exchangeName;

  public ExchangeApiKeysModel(
    UserManager<AppUser> userManager,
    IApiCredentialsRepository apiCredentialsRepository,
    IHttpClientFactory httpClientFactory,
    IOptions<TraderEngineApiSettings> apiSettings)
    : base(userManager)
  {
    _apiCredentialsRepository = apiCredentialsRepository;
    _httpClientFactory = httpClientFactory;
    _exchangeName = apiSettings.Value.ExchangeName;
  }

  [BindProperty]
  public ApiCredReqDto Input { get; set; } = new() { ApiKey = string.Empty, ApiSecret = string.Empty };

  private async Task<string> GetOutboundIp(CancellationToken ct)
  {
    try
    {
      using var client = _httpClientFactory.CreateClient("IpInfo");

      return await client.GetStringAsync("ip", ct);
    }
    catch (Exception)
    {
      return "unavailable";
    }
  }

  public async Task OnGetAsync(CancellationToken ct)
  {
    var user = await GetCurrentUserAsync();
    var status = await _apiCredentialsRepository.GetApiCredStatus(user.Id, _exchangeName);

    ViewData["ExchangeName"] = _exchangeName;
    ViewData["OutboundIp"] = await GetOutboundIp(ct);
    ViewData["CredentialStatus"] = status;
  }

  public async Task<IActionResult> OnPostAsync(CancellationToken ct)
  {
    var user = await GetCurrentUserAsync();

    if (!ModelState.IsValid)
    {
      ViewData["ExchangeName"] = _exchangeName;
      ViewData["OutboundIp"] = await GetOutboundIp(ct);
      ViewData["CredentialStatus"] = await _apiCredentialsRepository.GetApiCredStatus(user.Id, _exchangeName);

      return Page();
    }

    await _apiCredentialsRepository.SaveApiCred(user.Id, _exchangeName, Input);

    TempData["Notice"] = "API keys updated.";

    return RedirectToPage();
  }
}
