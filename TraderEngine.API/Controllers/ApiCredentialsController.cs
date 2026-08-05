using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Data.Repositories;

namespace TraderEngine.API.Controllers;

/// <summary>
/// Manages a user's stored, Data-Protection-encrypted exchange API credentials, used by
/// <see cref="Services.AutomationOrchestrator"/> to run scheduled automation cycles. Interactive
/// rebalance requests (<see cref="RebalanceController"/>) receive credentials directly from the
/// caller instead and never touch this store.
/// </summary>
[Authorize]
[ApiController, Route("api/[controller]")]
public class ApiCredentialsController : ControllerBase
{
  private readonly ILogger<ApiCredentialsController> _logger;
  private readonly IApiCredentialsRepository _apiCredentialsRepository;

  public ApiCredentialsController(
    ILogger<ApiCredentialsController> logger,
    IApiCredentialsRepository apiCredentialsRepository)
  {
    _logger = logger;
    _apiCredentialsRepository = apiCredentialsRepository;
  }

  [HttpPost("{exchangeName}")]
  public async Task<ActionResult> SaveApiCred(string exchangeName, ApiCredReqDto apiCredReqDto)
  {
    _logger.LogTrace("Handling SaveApiCred request for '{Host}' ..", HttpContext.Connection.RemoteIpAddress);

    // The user id is taken from the authenticated token's claims, never from a client-supplied
    // parameter — otherwise any caller could write encrypted credentials for any other user.
    var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    await _apiCredentialsRepository.SaveApiCred(userId, exchangeName, apiCredReqDto);

    return Ok();
  }
}
