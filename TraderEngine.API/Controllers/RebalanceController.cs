using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TraderEngine.API.Factories;
using TraderEngine.API.Services;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Exchanges;
using TraderEngine.Common.Mappers;
using TraderEngine.Common.Services;
using TraderEngine.Data.Repositories;

namespace TraderEngine.API.Controllers;

[ApiController, Route("api/[controller]"), EnableRateLimiting("trading")]
public class RebalanceController : ControllerBase
{
  // TODO: Put quote symbol for market cap records in appsettings.
  private readonly string _quoteSymbol = "EUR";

  private readonly ILogger<RebalanceController> _logger;
  private readonly ExchangeFactory _exchangeFactory;
  private readonly IRebalancingService _rebalancingService;
  private readonly IConfigRepository _configRepository;
  private readonly Func<IMarketCapService> _marketCapService;

  public RebalanceController(
    ILogger<RebalanceController> logger,
    IServiceProvider serviceProvider,
    ExchangeFactory exchangeFactory,
    IRebalancingService rebalancingService,
    IConfigRepository configRepository)
  {
    _logger = logger;
    _exchangeFactory = exchangeFactory;
    _rebalancingService = rebalancingService;
    _configRepository = configRepository;
    _marketCapService = serviceProvider.GetRequiredService<IMarketCapService>;
  }

  [HttpPost("simulate/{exchangeName}")]
  public async Task<ActionResult<SimulationDto>> SimulateRebalance(string exchangeName, string source, SimulationReqDto simulationReqDto)
  {
    _logger.LogTrace("Handling SimulateRebalance request for '{Host}' ..", HttpContext.Connection.RemoteIpAddress);

    var exchange = _exchangeFactory.GetService(exchangeName);

    if (exchange == null)
      return NotFound($"Exchange '{exchangeName}' not found.");

    var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var credentials = new ExchangeCredentials(simulationReqDto.ExchangeApiCred.ApiKey, simulationReqDto.ExchangeApiCred.ApiSecret, userId);

    // Get current balance.
    var balanceResult = await exchange.GetBalance(credentials);

    if (balanceResult.ErrorCode == ExchangeErrCodeEnum.AuthenticationError)
      return Unauthorized(balanceResult.Summary);

    if (balanceResult.ErrorCode != ExchangeErrCodeEnum.Ok)
      return StatusCode(500, balanceResult.Summary);

    var balance = balanceResult.Value!;

    var newAbsAllocs = simulationReqDto.NewAbsAllocs ??
      await _marketCapService().BalancedAbsAllocs(_quoteSymbol, simulationReqDto.Config, balance.Allocations.Select(alloc => alloc.Market).ToList());

    if (null == newAbsAllocs)
    {
      return NotFound("No recent market cap records found.");
    }

    // Filter for assets that are potentially tradable.
    var absAllocsTask = _rebalancingService.GetTopRankingAllocs(exchange, credentials, newAbsAllocs, simulationReqDto.Config.TopRankingCount);

    // Map here to retain current balance as it will be
    // modified by the simulation since it is passed by reference.
    var curBalanceDto = CommonMapper.MapBalance(balance);

    // Create mock exchange.
    var simExchange = new SimExchange(exchange, balance);

    // Await for the task to complete.
    var absAllocs = await absAllocsTask;

    // Simulate rebalance. SimExchange/MockExchange resolve a Limit order's price from the cached
    // allocation price (no real order book lookup) and fill it instantly at the maker rate, so
    // UseLimitOrders is honored here too — the preview's estimated fees stay accurate without
    // ever touching a real API for a placement that will never actually rest.
    var orders = await _rebalancingService.Rebalance(simExchange, credentials, simulationReqDto.Config, absAllocs, balance, source);

    // NOTE: This is not needed because the balance is passed by reference.
    //var newBalance = await simExchange.GetBalance(credentials);
    var newBalanceDto = CommonMapper.MapBalance(balance);

    return Ok(new SimulationDto()
    {
      Config = simulationReqDto.Config,
      Orders = orders,
      NewAbsAllocs = absAllocs,
      CurBalance = curBalanceDto,
      NewBalance = newBalanceDto,
    });
  }

  [HttpPost("{exchangeName}")]
  public async Task<ActionResult<OrderDto[]>> Rebalance(string exchangeName, string source, RebalanceReqDto rebalanceReqDto)
  {
    _logger.LogTrace("Handling Rebalance request for '{Host}' ..", HttpContext.Connection.RemoteIpAddress);

    var exchange = _exchangeFactory.GetService(exchangeName);

    if (exchange == null)
      return NotFound($"Exchange '{exchangeName}' not found.");

    var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var credentials = new ExchangeCredentials(rebalanceReqDto.ExchangeApiCred.ApiKey, rebalanceReqDto.ExchangeApiCred.ApiSecret, userId);

    // Filter for assets that are potentially tradable.
    var absAllocs = await _rebalancingService.GetTopRankingAllocs(exchange, credentials, rebalanceReqDto.NewAbsAllocs, rebalanceReqDto.Config.TopRankingCount);

    // Execute rebalance.
    // TODO: Properly handle exchange auth errors.
    var orders = await _rebalancingService.Rebalance(exchange, credentials, rebalanceReqDto.Config, absAllocs, null, source);

    // Persisted here rather than left to the caller (TraderEngine.Web used to set this only after
    // a successful round trip): the rebalance has already run for real by this point regardless of
    // whether the calling client is still around to receive the response (e.g. the browser tab was
    // closed, or the client-side HTTP timeout elapsed, mid-request) — see the "not yet implemented"
    // notes on that failure mode in CLAUDE.md.
    rebalanceReqDto.Config.LastRebalance = DateTime.UtcNow;
    await _configRepository.SaveConfig(userId, rebalanceReqDto.Config);

    return Ok(orders);
  }

  [HttpPost("execute/{exchangeName}")]
  public async Task<ActionResult<OrderDto[]>> ExecuteOrders(string exchangeName, string source, ExecuteOrdersReqDto executeOrdersReqDto)
  {
    _logger.LogTrace("Handling ExecuteOrders request for '{Host}' ..", HttpContext.Connection.RemoteIpAddress);

    var exchange = _exchangeFactory.GetService(exchangeName);

    if (exchange == null)
      return NotFound($"Exchange '{exchangeName}' not found.");

    var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var credentials = new ExchangeCredentials(executeOrdersReqDto.ExchangeApiCred.ApiKey, executeOrdersReqDto.ExchangeApiCred.ApiSecret, userId);

    // Execute rebalance orders.
    // TODO: Properly handle exchange auth errors.
    var orders = await _rebalancingService.Rebalance(exchange, credentials, executeOrdersReqDto.Orders, source);

    return Ok(orders);
  }
}
