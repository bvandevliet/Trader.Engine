using TraderEngine.Common.DTOs.API.Request;

namespace TraderEngine.Common.DTOs.API.Response;

public class SimulationDto
{
  public OrderDto[] Orders { get; set; } = Array.Empty<OrderDto>();

  public decimal TotalFee => Orders.Sum(order => order.FeePaid);

  public BalanceDto CurBalance { get; set; } = null!;

  public BalanceDto NewBalance { get; set; } = null!;

  public IEnumerable<AbsAllocReqDto> NewAbsAllocsTradable { get; set; } = null!;
}