using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TraderEngine.API.Factories;
using TraderEngine.API.Services;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Exchanges;
using TraderEngine.Common.Mappers;

namespace TraderEngine.API.Controllers;

[ApiController, Route("api/[controller]"), EnableRateLimiting("trading")]
public class AllocationsController : ControllerBase
{
  // TODO: Put quote symbol for market cap records in appsettings.
  private readonly string _quoteSymbol = "EUR";

  private readonly ILogger<AllocationsController> _logger;
  private readonly ExchangeFactory _exchangeFactory;
  private readonly Func<IMarketCapService> _marketCapService;

  public AllocationsController(
    ILogger<AllocationsController> logger,
    IServiceProvider serviceProvider,
    ExchangeFactory exchangeFactory)
  {
    _logger = logger;
    _exchangeFactory = exchangeFactory;
    _marketCapService = serviceProvider.GetRequiredService<IMarketCapService>;
  }

  [HttpPost("current/{exchangeName}")]
  public async Task<ActionResult<BalanceDto>> CurrentBalance(string exchangeName, ApiCredReqDto apiCredReqDto)
  {
    _logger.LogTrace("Handling CurrentBalance request for '{Host}' ..", HttpContext.Connection.RemoteIpAddress);

    var exchange = _exchangeFactory.GetService(exchangeName);

    if (exchange == null)
      return NotFound($"Exchange '{exchangeName}' not found.");

    var credentials = new ExchangeCredentials(apiCredReqDto.ApiKey, apiCredReqDto.ApiSecret);

    var balanceResult = await exchange.GetBalance(credentials);

    return balanceResult.ErrorCode switch
    {
      ExchangeErrCodeEnum.AuthenticationError => Unauthorized(balanceResult.Summary),
      ExchangeErrCodeEnum.Ok => Ok(CommonMapper.MapBalance(balanceResult.Value!)),
      _ => StatusCode(500, balanceResult.Summary)
    };
  }

  [HttpPost("balanced")]
  public async Task<ActionResult<List<AbsAllocReqDto>>> BalancedAbsAllocs(ConfigReqDto configReqDto)
  {
    _logger.LogTrace(
      "Handling BalancedAbsAllocs request for '{Host}' ..", HttpContext.Connection.RemoteIpAddress);

    var absAllocs = await _marketCapService()
      .BalancedAbsAllocs(_quoteSymbol, configReqDto);

    return absAllocs == null
      ? NotFound("No recent market cap records found.")
      : Ok(absAllocs);
  }
}