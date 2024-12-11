using Microsoft.AspNetCore.Mvc;
using TraderEngine.API.Factories;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.Enums;

namespace TraderEngine.API.Controllers;

[ApiController, Route("api/[controller]")]
public class AccountController : ControllerBase
{
  private readonly ILogger<AccountController> _logger;
  private readonly ExchangeFactory _exchangeFactory;

  public AccountController(
    ILogger<AccountController> logger,
    ExchangeFactory exchangeFactory)
  {
    _logger = logger;
    _exchangeFactory = exchangeFactory;
  }

  [HttpPost("totals/deposited/{exchangeName}")]
  public async Task<ActionResult<decimal>> TotalDeposited(string exchangeName, ApiCredReqDto apiCredentials)
  {
    _logger.LogDebug("Handling TotalDeposited request for '{Host}' ..", HttpContext.Connection.RemoteIpAddress);

    var exchange = _exchangeFactory.GetService(exchangeName);

    exchange.ApiKey = apiCredentials.ApiKey;
    exchange.ApiSecret = apiCredentials.ApiSecret;

    var totalDepositedResult = await exchange.TotalDeposited();

    return totalDepositedResult.ErrorCode switch
    {
      ExchangeErrCodeEnum.AuthenticationError => Unauthorized(totalDepositedResult.Summary),
      ExchangeErrCodeEnum.Ok => Ok(totalDepositedResult.Value),
      _ => BadRequest(totalDepositedResult.Summary)
    };
  }

  [HttpPost("totals/withdrawn/{exchangeName}")]
  public async Task<ActionResult<decimal>> TotalWithdrawn(string exchangeName, ApiCredReqDto apiCredentials)
  {
    _logger.LogDebug("Handling TotalWithdrawn request for '{Host}' ..", HttpContext.Connection.RemoteIpAddress);

    var exchange = _exchangeFactory.GetService(exchangeName);

    exchange.ApiKey = apiCredentials.ApiKey;
    exchange.ApiSecret = apiCredentials.ApiSecret;

    var totalWithdrawnResult = await exchange.TotalWithdrawn();

    return totalWithdrawnResult.ErrorCode switch
    {
      ExchangeErrCodeEnum.AuthenticationError => Unauthorized(totalWithdrawnResult.Summary),
      ExchangeErrCodeEnum.Ok => Ok(totalWithdrawnResult.Value),
      _ => BadRequest(totalWithdrawnResult.Summary)
    };
  }
}
