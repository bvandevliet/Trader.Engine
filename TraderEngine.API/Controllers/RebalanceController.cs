using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using TraderEngine.API.Exchanges;
using TraderEngine.API.Extensions;
using TraderEngine.API.Factories;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Models;

namespace TraderEngine.API.Controllers;

[ApiController, Route("api/[controller]")]
public class RebalanceController : ControllerBase
{
  private readonly ILogger<RebalanceController> _logger;
  private readonly IMapper _mapper;
  private readonly ExchangeFactory _exchangeFactory;

  public RebalanceController(
    ILogger<RebalanceController> logger,
    IMapper mapper,
    ExchangeFactory exchangeFactory)
  {
    _logger = logger;
    _mapper = mapper;
    _exchangeFactory = exchangeFactory;
  }

  [HttpPost("simulate/{exchangeName}")]
  public async Task<ActionResult<RebalanceDto>> SimulateRebalance(string exchangeName, RebalanceReqDto rebalanceReqDto)
  {
    _logger.LogInformation("Handling SimulateRebalance request for '{Host}'.", HttpContext.Connection.RemoteIpAddress);

    var exchange = _exchangeFactory.GetService(exchangeName);

    exchange.ApiKey = rebalanceReqDto.ExchangeApiCred.ApiKey;
    exchange.ApiSecret = rebalanceReqDto.ExchangeApiCred.ApiSecret;

    var curBalance = await exchange.GetBalance();

    var simExchange = new SimExchange(exchange, curBalance);

    var orders = null != rebalanceReqDto.AllocDiffs
      ? await simExchange.Rebalance(rebalanceReqDto.NewAbsAllocs, rebalanceReqDto.AllocDiffs)
      : await simExchange.Rebalance(rebalanceReqDto.NewAbsAllocs, curBalance);

    var newBalance = await simExchange.GetBalance();

    return Ok(new RebalanceDto()
    {
      Orders = orders,
      NewBalance = _mapper.Map<BalanceDto>(newBalance),
    });
  }

  [HttpPost("execute/{exchangeName}")]
  public async Task<ActionResult<RebalanceDto>> ExecuteRebalance(string exchangeName, RebalanceReqDto rebalanceReqDto)
  {
    _logger.LogInformation("Handling ExecuteRebalance request for '{Host}'.", HttpContext.Connection.RemoteIpAddress);

    var exchange = _exchangeFactory.GetService(exchangeName);

    exchange.ApiKey = rebalanceReqDto.ExchangeApiCred.ApiKey;
    exchange.ApiSecret = rebalanceReqDto.ExchangeApiCred.ApiSecret;

    OrderDto[] orders;

    Balance? newBalance;

    // If allocation diffs are provided, there is no need to get the current balance.
    if (null != rebalanceReqDto.AllocDiffs)
    {
      orders = await exchange.Rebalance(rebalanceReqDto.NewAbsAllocs, rebalanceReqDto.AllocDiffs);

      newBalance = await exchange.GetBalance();
    }
    // Else, get the current balance and calculate the new balance using the simulated exchange to reduce API calls.
    else
    {
      var curBalance = await exchange.GetBalance();

      orders = await exchange.Rebalance(rebalanceReqDto.NewAbsAllocs, curBalance);

      var simExchange = new SimExchange(exchange, curBalance);

      await simExchange.ProcessOrders(orders);

      newBalance = await simExchange.GetBalance();
    }

    return Ok(new RebalanceDto()
    {
      Orders = orders,
      NewBalance = _mapper.Map<BalanceDto>(newBalance),
    });
  }
}
