using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using TraderEngine.API.Exchanges;
using TraderEngine.API.Extensions;
using TraderEngine.API.Factories;
using TraderEngine.API.Services;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Enums;
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
    Balance balance;

    if (null == absAllocs)
    {
      var absAllocsTask = _marketCapService()
        .BalancedAbsAllocs(_quoteSymbol, simulationReqDto.Config);

      var curBalanceTask = exchange.GetBalance();

      await Task.WhenAll(absAllocsTask, curBalanceTask);

      absAllocs = absAllocsTask.Result;
      balance = curBalanceTask.Result;

      if (null == absAllocs)
        return NotFound("No recent market cap records found.");
    }
    else
    {
      balance = await exchange.GetBalance();
    }

    // Map here to retain current balance as it will be
    // modified by the simulation since it is passed by reference.
    var curBalanceDto = _mapper.Map<BalanceDto>(balance);

    var simExchange = new SimExchange(exchange, balance);

    // Get absolute balanced allocations for tradable assets.
    // TODO: Get and set price for assets that we do not know price of yet (those not in current balance) ??
    var allocsMarketDataTasks = absAllocs.Select(async absAlloc =>
    {
      var marketDto = new MarketReqDto(exchange.QuoteSymbol, absAlloc.BaseSymbol);

      return new { absAlloc, marketData = await exchange.GetMarket(marketDto) };
    });

    // Wait for all tasks to complete.
    var allocsMarketData = await Task.WhenAll(allocsMarketDataTasks);

    // Filter for assets that are tradable.
    var absAllocsTradable = allocsMarketData
      .Where(x => x.marketData?.Status == MarketStatus.Trading)
      .Select(x => x.absAlloc).ToList();

    // Simulate rebalance.
    var orders = await simExchange.Rebalance(simulationReqDto.Config, absAllocsTradable, balance);

    //var newBalance = await simExchange.GetBalance();
    var newBalanceDto = _mapper.Map<BalanceDto>(balance);

    return Ok(new SimulationDto()
    {
      Orders = orders,
      NewAbsAllocsTradable = absAllocsTradable,
      CurBalance = curBalanceDto,
      NewBalance = newBalanceDto,
    });
  }

  [HttpPost("{exchangeName}")]
  public async Task<ActionResult<OrderDto[]>> Rebalance(string exchangeName, RebalanceReqDto rebalanceReqDto)
  {
    _logger.LogDebug("Handling Rebalance request for '{Host}' ..", HttpContext.Connection.RemoteIpAddress);

    var exchange = _exchangeFactory.GetService(exchangeName);

    exchange.ApiKey = rebalanceReqDto.ExchangeApiCred.ApiKey;
    exchange.ApiSecret = rebalanceReqDto.ExchangeApiCred.ApiSecret;

    // Execute rebalance.
    var orders = await exchange.Rebalance(rebalanceReqDto.Config, rebalanceReqDto.NewAbsAllocs);

    return Ok(orders);
  }

  [HttpPost("execute/{exchangeName}")]
  public async Task<ActionResult<OrderDto[]>> ExecuteOrders(string exchangeName, ExecuteOrdersReqDto executeOrdersReqDto)
  {
    _logger.LogDebug("Handling ExecuteOrders request for '{Host}' ..", HttpContext.Connection.RemoteIpAddress);

    var exchange = _exchangeFactory.GetService(exchangeName);

    exchange.ApiKey = executeOrdersReqDto.ExchangeApiCred.ApiKey;
    exchange.ApiSecret = executeOrdersReqDto.ExchangeApiCred.ApiSecret;

    // Execute rebalance orders.
    var orders = await exchange.Rebalance(executeOrdersReqDto.Orders);

    return Ok(orders);
  }
}
