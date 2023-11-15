using Microsoft.AspNetCore.Mvc;
using TraderEngine.API.Factories;
using TraderEngine.Common.DTOs.API.Request;

namespace TraderEngine.API.Controllers;

[ApiController, Route("api/[controller]")]
public class AccountController : ControllerBase
{
  private readonly ExchangeFactory _exchangeFactory;

  public AccountController(ExchangeFactory exchangeFactory)
  {
    _exchangeFactory = exchangeFactory;
  }

  [HttpPost("totals/deposited/{exchangeName}")]
  public async Task<ActionResult<decimal>> TotalDeposited(string exchangeName, ApiCredReqDto apiCredentials)
  {
    var exchange = _exchangeFactory.GetService(exchangeName);

    exchange.ApiKey = apiCredentials.ApiKey;
    exchange.ApiSecret = apiCredentials.ApiSecret;

    decimal? balance = await exchange.TotalDeposited();

    return null == balance ? NotFound() : Ok(balance);
  }

  [HttpPost("totals/withdrawn/{exchangeName}")]
  public async Task<ActionResult<decimal>> TotalWithdrawn(string exchangeName, ApiCredReqDto apiCredentials)
  {
    var exchange = _exchangeFactory.GetService(exchangeName);

    exchange.ApiKey = apiCredentials.ApiKey;
    exchange.ApiSecret = apiCredentials.ApiSecret;

    decimal? balance = await exchange.TotalWithdrawn();

    return null == balance ? NotFound() : Ok(balance);
  }
}
