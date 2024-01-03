using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using TraderEngine.API.Factories;
using TraderEngine.API.Services;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Enums;

namespace TraderEngine.API.Controllers;

[ApiController, Route("api/[controller]")]
public class AllocationsController : ControllerBase
{
  private readonly ILogger<AllocationsController> _logger;
  private readonly IMapper _mapper;
  private readonly ExchangeFactory _exchangeFactory;
  private readonly Func<IMarketCapService> _marketCapService;

  public AllocationsController(
    ILogger<AllocationsController> logger,
    IServiceProvider serviceProvider,
    IMapper mapper,
    ExchangeFactory exchangeFactory)
  {
    _logger = logger;
    _mapper = mapper;
    _exchangeFactory = exchangeFactory;
    _marketCapService = serviceProvider.GetRequiredService<IMarketCapService>;
  }

  [HttpPost("current/{exchangeName}")]
  public async Task<ActionResult<BalanceDto>> CurrentBalance(string exchangeName, ApiCredReqDto apiCredentials)
  {
    _logger.LogDebug("Handling CurrentBalance request for '{Host}' ..", HttpContext.Connection.RemoteIpAddress);

    var exchange = _exchangeFactory.GetService(exchangeName);

    exchange.ApiKey = apiCredentials.ApiKey;
    exchange.ApiSecret = apiCredentials.ApiSecret;

    var balance = await exchange.GetBalance();

    return Ok(_mapper.Map<BalanceDto>(balance));
  }

  [HttpPost("balanced/{exchangeName}")]
  public async Task<ActionResult<List<AbsAllocReqDto>>> BalancedAbsAllocs(string exchangeName, BalancedReqDto balancedReqDto)
  {
    _logger.LogDebug("Handling BalancedAbsAllocs request for '{Host}' ..", HttpContext.Connection.RemoteIpAddress);

    // Get absolute balanced allocations.
    var absAllocs = await _marketCapService()
      .BalancedAbsAllocs(balancedReqDto.QuoteSymbol, balancedReqDto.Config);

    if (null == absAllocs)
    {
      return NotFound("No recent market cap records found.");
    }

    var exchange = _exchangeFactory.GetService(exchangeName);

    exchange.ApiKey = balancedReqDto.ExchangeApiCred.ApiKey;
    exchange.ApiSecret = balancedReqDto.ExchangeApiCred.ApiSecret;

    // Get absolute balanced allocation tasks, to check if tradable.
    var allocsMarketDataTasks = absAllocs.Select(async absAlloc =>
    {
      var marketDto = new MarketReqDto(balancedReqDto.QuoteSymbol, absAlloc.BaseSymbol);

      return new { absAlloc, marketData = await exchange.GetMarket(marketDto) };
    });

    // Wait for all tasks to complete.
    var allocsMarketData = await Task.WhenAll(allocsMarketDataTasks);

    // Relative quote allocation.
    decimal quoteRelAlloc = Math.Max(0, Math.Min(1,
      balancedReqDto.Config.QuoteTakeout / balancedReqDto.AmountQuoteTotal + balancedReqDto.Config.QuoteAllocation / 100));

    // Sum of all absolute allocation values.
    decimal totalAbsAlloc = 0;

    // Absolute allocations to be used for rebalancing.
    var absAllocsList = allocsMarketData
      // Filter for assets that are tradable.
      .Where(x => x.marketData?.Status == MarketStatus.Trading)
      // Scale absolute allocation values to include relative quote allocation.
      .Select(x =>
      {
        totalAbsAlloc += x.absAlloc.AbsAlloc;

        x.absAlloc.AbsAlloc *= (1 - quoteRelAlloc);

        return x.absAlloc;
      })
      // As list since we're using a summed total.
      .ToList();

    // Add quote allocation.
    absAllocsList.Add(new AbsAllocReqDto(balancedReqDto.QuoteSymbol, totalAbsAlloc * quoteRelAlloc));

    return Ok(absAllocsList);
  }
}
