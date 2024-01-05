using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using TraderEngine.API.Factories;
using TraderEngine.API.Services;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;

namespace TraderEngine.API.Controllers;

[ApiController, Route("api/[controller]")]
public class AllocationsController : ControllerBase
{
  // TODO: Put quote symbol for market cap records in appsettings.
  private readonly string _quoteSymbol = "EUR";

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
  public async Task<ActionResult<BalanceDto>> CurrentBalance(string exchangeName, ApiCredReqDto apiCredReqDto)
  {
    _logger.LogDebug("Handling CurrentBalance request for '{Host}' ..", HttpContext.Connection.RemoteIpAddress);

    var exchange = _exchangeFactory.GetService(exchangeName);

    exchange.ApiKey = apiCredReqDto.ApiKey;
    exchange.ApiSecret = apiCredReqDto.ApiSecret;

    var balance = await exchange.GetBalance();

    return Ok(_mapper.Map<BalanceDto>(balance));
  }

  [HttpPost("balanced")]
  public async Task<ActionResult<List<AbsAllocReqDto>>> BalancedAbsAllocs(ConfigReqDto configReqDto)
  {
    _logger.LogDebug(
      "Handling BalancedAbsAllocs request for '{Host}' ..", HttpContext.Connection.RemoteIpAddress);

    var absAllocs = await _marketCapService()
      .BalancedAbsAllocs(_quoteSymbol, configReqDto);

    return absAllocs != null ? Ok(absAllocs) : NotFound("No recent market cap records found.");
  }
}