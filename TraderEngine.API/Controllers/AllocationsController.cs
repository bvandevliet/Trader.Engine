using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using TraderEngine.API.Exchanges;
using TraderEngine.API.Factories;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Models;
using TraderEngine.Common.Services;

namespace TraderEngine.API.Controllers;

[ApiController, Route("api/[controller]")]
public class AllocationsController : ControllerBase
{
  private readonly IMapper _mapper;
  private readonly IServiceProvider _serviceProvider;
  private readonly ExchangeFactory _exchangeFactory;

  public AllocationsController(
    IMapper mapper,
    IServiceProvider serviceProvider,
    ExchangeFactory exchangeFactory)
  {
    _mapper = mapper;
    _serviceProvider = serviceProvider;
    _exchangeFactory = exchangeFactory;
  }

  [HttpPost("current/{exchangeName}")]
  public async Task<ActionResult<BalanceDto>> CurrentBalance(string exchangeName, ExchangeReqDto exchangeReqDto)
  {
    IExchange exchange = _exchangeFactory.GetService(exchangeName);

    exchange.ApiKey = exchangeReqDto.ApiKey;
    exchange.ApiSecret = exchangeReqDto.ApiSecret;

    Balance balance = await exchange.GetBalance();

    return Ok(_mapper.Map<BalanceDto>(balance));
  }

  [HttpGet("balanced")]
  public async Task<ActionResult<List<AbsAllocReqDto>>> BalancedAllocations(ConfigReqDto configReqDto)
  {
    // Not injected in ctor because it's only used here.
    IMarketCapService marketCapService = _serviceProvider.GetRequiredService<IMarketCapService>();

    IEnumerable<AbsAllocReqDto> marketCapLatest =
      await marketCapService.BalancedAllocations(configReqDto, false);

    return Ok(marketCapLatest);
  }
}
