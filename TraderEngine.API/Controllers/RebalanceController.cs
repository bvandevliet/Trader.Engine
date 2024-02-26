using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using TraderEngine.API.Exchanges;
using TraderEngine.API.Extensions;
using TraderEngine.API.Factories;
using TraderEngine.API.Services;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Enums;

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

  private static Task<AbsAllocReqDto[]> FetchMarketStatus(IExchange exchange, IEnumerable<AbsAllocReqDto> absAllocs)
  {
    // Get market data for all assets and update market status.
    var allocsMarketDataTasks = absAllocs.Select(async absAlloc =>
    {
      if (absAlloc.MarketStatus == MarketStatus.Unknown)
      {
        var marketDto = new MarketReqDto(exchange.QuoteSymbol, absAlloc.Market.BaseSymbol);

        var marketData = await exchange.GetMarket(marketDto);

        absAlloc.MarketStatus = marketData?.Status ?? MarketStatus.Unknown;
      }

      return absAlloc;
    });

    // Wait for all tasks to complete.
    return Task.WhenAll(allocsMarketDataTasks);
  }

  [HttpPost("simulate/{exchangeName}")]
  public async Task<ActionResult<SimulationDto>> SimulateRebalance(string exchangeName, SimulationReqDto simulationReqDto)
  {
    _logger.LogDebug("Handling SimulateRebalance request for '{Host}' ..", HttpContext.Connection.RemoteIpAddress);

    var absAllocs = simulationReqDto.NewAbsAllocs ??
      await _marketCapService().BalancedAbsAllocs(_quoteSymbol, simulationReqDto.Config);

    if (null == absAllocs)
    {
      return NotFound("No recent market cap records found.");
    }

    var exchange = _exchangeFactory.GetService(exchangeName);

    exchange.ApiKey = simulationReqDto.ExchangeApiCred.ApiKey;
    exchange.ApiSecret = simulationReqDto.ExchangeApiCred.ApiSecret;

    // Get market data for all assets and update market status.
    var absAllocsUpdateTask = FetchMarketStatus(exchange, absAllocs);

    // Get current balance.
    var balanceResult = await exchange.GetBalance();

    if (balanceResult.ErrorCode == ExchangeErrCodeEnum.AuthenticationError)
    {
      return Unauthorized(balanceResult.ErrorMessage);
    }

    var balance = balanceResult.Value!;

    // Map here to retain current balance as it will be
    // modified by the simulation since it is passed by reference.
    var curBalanceDto = _mapper.Map<BalanceDto>(balance);

    // Create mock exchange.
    var simExchange = new SimExchange(exchange, balance);

    // Filter for assets that are potentially tradable.
    var absAllocsTradable = (await absAllocsUpdateTask)
      .Where(absAlloc => absAlloc.MarketStatus is not MarketStatus.Unknown and not MarketStatus.Unavailable)
      .ToList();

    // Simulate rebalance.
    var orders = await simExchange.Rebalance(simulationReqDto.Config, absAllocsTradable, balance);

    // NOTE: This is not needed because the balance is passed by reference.
    //var newBalance = await simExchange.GetBalance();
    var newBalanceDto = _mapper.Map<BalanceDto>(balance);

    return Ok(new SimulationDto()
    {
      Orders = orders,
      NewAbsAllocs = absAllocsTradable,
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

    // Get market data for all assets and update market status.
    var absAllocsUpdated = await FetchMarketStatus(exchange, rebalanceReqDto.NewAbsAllocs);

    // Filter for assets that are potentially tradable.
    var absAllocsTradable = absAllocsUpdated
      .Where(absAlloc => absAlloc.MarketStatus is not MarketStatus.Unknown and not MarketStatus.Unavailable)
      .ToList();

    // Execute rebalance.
    // TODO: Properly handle exchange auth errors.
    var orders = await exchange.Rebalance(rebalanceReqDto.Config, absAllocsTradable);

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
    // TODO: Properly handle exchange auth errors.
    var orders = await exchange.Rebalance(executeOrdersReqDto.Orders);

    return Ok(orders);
  }
}
