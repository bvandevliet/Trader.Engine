using Microsoft.AspNetCore.Mvc;
using TraderEngine.API.Factories;
using TraderEngine.Common.DTOs.API.Request;

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
    _logger.LogInformation("Handling TotalDeposited request for '{Host}'.", HttpContext.Connection.RemoteIpAddress);

    var exchange = _exchangeFactory.GetService(exchangeName);

    exchange.ApiKey = apiCredentials.ApiKey;
    exchange.ApiSecret = apiCredentials.ApiSecret;

    decimal? balance = await exchange.TotalDeposited();

    return null == balance ? NotFound() : Ok(balance);
  }

  [HttpPost("totals/withdrawn/{exchangeName}")]
  public async Task<ActionResult<decimal>> TotalWithdrawn(string exchangeName, ApiCredReqDto apiCredentials)
  {
    _logger.LogInformation("Handling TotalWithdrawn request for '{Host}'.", HttpContext.Connection.RemoteIpAddress);

    var exchange = _exchangeFactory.GetService(exchangeName);

    exchange.ApiKey = apiCredentials.ApiKey;
    exchange.ApiSecret = apiCredentials.ApiSecret;

    decimal? balance = await exchange.TotalWithdrawn();

    return null == balance ? NotFound() : Ok(balance);
  }
}
