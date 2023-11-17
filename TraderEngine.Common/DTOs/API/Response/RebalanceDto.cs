namespace TraderEngine.Common.DTOs.API.Response;

public class RebalanceDto
{
  public IEnumerable<OrderDto> Orders { get; set; } = new List<OrderDto>();

  public decimal TotalFee { get; set; }

  public BalanceDto NewBalance { get; set; } = null!;
}