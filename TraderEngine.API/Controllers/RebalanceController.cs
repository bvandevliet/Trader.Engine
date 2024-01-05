using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using TraderEngine.API.Exchanges;
using TraderEngine.API.Extensions;
using TraderEngine.API.Factories;
using TraderEngine.API.Services;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Models;

namespace TraderEngine.API.Controllers;

[ApiController, Route("api/[controller]")]
public class RebalanceController : ControllerBase
{
  // TODO: Put quote symbol for market cap records in appsettings.
  private readonly string _quoteSymbol = "EUR";

  private readonly ILogger<RebalanceController> _logger;
  private readonly IMapper _mapper;
  private readonly ExchangeFactory _exchangeFactory;
  private readonly Func<IMarketCapService> _marketCapService;

  public RebalanceController(
    ILogger<RebalanceController> logger,
    IServiceProvider serviceProvider,
    IMapper mapper,
    ExchangeFactory exchangeFactory)
  {
    _logger = logger;
    _mapper = mapper;
    _exchangeFactory = exchangeFactory;
    _marketCapService = serviceProvider.GetRequiredService<IMarketCapService>;
  }

  [HttpPost("simulate/{exchangeName}")]
  public async Task<ActionResult<SimulationDto>> SimulateRebalance(string exchangeName, SimulationReqDto simulationReqDto)
  {
    _logger.LogDebug("Handling SimulateRebalance request for '{Host}' ..", HttpContext.Connection.RemoteIpAddress);

    var exchange = _exchangeFactory.GetService(exchangeName);

    exchange.ApiKey = simulationReqDto.ExchangeApiCred.ApiKey;
    exchange.ApiSecret = simulationReqDto.ExchangeApiCred.ApiSecret;

    var absAllocs = simulationReqDto.NewAbsAllocs;
    Balance curBalance;

    if (null == absAllocs)
    {
      var absAllocsTask = _marketCapService()
        .BalancedAbsAllocs(_quoteSymbol, simulationReqDto.Config);

      var curBalanceTask = exchange.GetBalance();

      await Task.WhenAll(absAllocsTask, curBalanceTask);

      absAllocs = absAllocsTask.Result;
      curBalance = curBalanceTask.Result;

      if (null == absAllocs)
        return NotFound("No recent market cap records found.");
    }
    else
    {
      curBalance = await exchange.GetBalance();
    }

    var simExchange = new SimExchange(exchange, curBalance);

    var orders = await simExchange.Rebalance(absAllocs, simulationReqDto.Config, curBalance);

    var newBalance = await simExchange.GetBalance();

    return Ok(new SimulationDto()
    {
      Orders = orders,
      CurBalance = _mapper.Map<BalanceDto>(curBalance),
      NewBalance = _mapper.Map<BalanceDto>(newBalance),
    });
  }

  [HttpPost("execute/{exchangeName}")]
  public async Task<ActionResult<OrderDto[]>> ExecuteRebalance(string exchangeName, RebalanceReqDto rebalanceReqDto)
  {
    _logger.LogDebug("Handling ExecuteRebalance request for '{Host}' ..", HttpContext.Connection.RemoteIpAddress);

    var exchange = _exchangeFactory.GetService(exchangeName);

    exchange.ApiKey = rebalanceReqDto.ExchangeApiCred.ApiKey;
    exchange.ApiSecret = rebalanceReqDto.ExchangeApiCred.ApiSecret;

    var orders = await exchange.Rebalance(rebalanceReqDto.Orders);

    return Ok(orders);
  }
}
