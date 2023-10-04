using Microsoft.AspNetCore.Mvc;
using TraderEngine.API.Factories;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;

namespace TraderEngine.API.Controllers;

[ApiController, Route("api/[controller]")]
public class RebalanceController : ControllerBase
{
  private readonly ExchangeFactory _exchangeFactory;

  public RebalanceController(ExchangeFactory exchangeFactory)
  {
    _exchangeFactory = exchangeFactory;
  }

  [HttpPost("simulate/{exchangeName}")]
  public Task<ActionResult<RebalanceDto>> SimulateRebalance(string exchangeName, RebalanceReqDto rebalanceReqDto)
  {
    throw new NotImplementedException();
  }

  [HttpPost("rebalance/{exchangeName}")]
  public Task<ActionResult<RebalanceDto>> ExecuteRebalance(string exchangeName, RebalanceReqDto rebalanceReqDto)
  {
    throw new NotImplementedException();
  }
}
