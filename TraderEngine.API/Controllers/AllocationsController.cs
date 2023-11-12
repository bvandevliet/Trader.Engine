using AutoMapper;
using Microsoft.AspNetCore.Mvc;
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
  private readonly ExchangeFactory _exchangeFactory;
  private readonly Func<IMarketCapService> _marketCapService;

  public AllocationsController(
    IServiceProvider serviceProvider,
    IMapper mapper,
    ExchangeFactory exchangeFactory)
  {
    _mapper = mapper;
    _exchangeFactory = exchangeFactory;
    _marketCapService = serviceProvider.GetRequiredService<IMarketCapService>;
  }

  [HttpPost("current/{exchangeName}")]
  public async Task<ActionResult<BalanceDto>> CurrentBalance(string exchangeName, ApiCredReqDto apiCredentials)
  {
    var exchange = _exchangeFactory.GetService(exchangeName);

    exchange.ApiKey = apiCredentials.ApiKey;
    exchange.ApiSecret = apiCredentials.ApiSecret;

    var balance = await exchange.GetBalance();

    return Ok(_mapper.Map<BalanceDto>(balance));
  }

  [HttpPost("balanced/{exchangeName}")]
  public async Task<ActionResult<List<AbsAllocReqDto>>> BalancedAbsAllocs(string exchangeName, BalanceReqDto balancedReqDto)
  {
    var exchange = _exchangeFactory.GetService(exchangeName);

    exchange.ApiKey = balancedReqDto.ExchangeApiCred.ApiKey;
    exchange.ApiSecret = balancedReqDto.ExchangeApiCred.ApiSecret;

    // Get current balance object if needed.
    Balance? curBalance = null;
    if (null == balancedReqDto.QuoteSymbol || null == balancedReqDto.AmountQuoteTotal)
    {
      curBalance = await exchange.GetBalance();

      if (null == curBalance)
      {
        // TODO: MAKE 502 !!
        return BadRequest("Required data not provided and could not be determined from exchange.");
      }
    }

    string quoteSymbol =
      curBalance?.QuoteSymbol ?? balancedReqDto.QuoteSymbol ?? curBalance!.QuoteSymbol;
    decimal amountQuoteTotal =
      curBalance?.AmountQuoteTotal ?? balancedReqDto.AmountQuoteTotal ?? curBalance!.AmountQuoteTotal;

    // Get absolute balanced allocations.
    var absAllocs = await _marketCapService()
      .BalancedAbsAllocs(quoteSymbol, balancedReqDto.Config);

    // Get absolute balanced allocation tasks, to check if tradable.
    var allocsMarketDataTasks = absAllocs.Select(async absAlloc =>
    {
      var marketDto = new MarketReqDto(quoteSymbol, absAlloc.BaseSymbol);

      return new { absAlloc, marketData = await exchange.GetMarket(marketDto) };
    });

    // Wait for all tasks to complete.
    var allocsMarketData = await Task.WhenAll(allocsMarketDataTasks);

    // Sum of all absolute allocation values.
    decimal totalAbsAlloc = 0;

    // Absolute allocations to be used for rebalancing.
    var absAllocsList = allocsMarketData
      // Filter for assets that are tradable.
      .Where(x => x.marketData?.Status == "trading")
      .Select(x =>
      {
        totalAbsAlloc += x.absAlloc.AbsAlloc;

        return x.absAlloc;
      })
      // As list since we're using a summed total.
      .ToList();

    // Add quote allocation.
    absAllocsList.Add(new AbsAllocReqDto(quoteSymbol,
      totalAbsAlloc / (1 - (balancedReqDto.Config.QuoteTakeout / amountQuoteTotal + balancedReqDto.Config.QuoteAllocation / 100)) - totalAbsAlloc));

    return Ok(absAllocsList);
  }
}
