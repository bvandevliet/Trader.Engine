namespace TraderEngine.Common.DTOs.API.Response;

public class RebalanceDto
{
  public List<OrderDto> Orders { get; set; } = new();

  public decimal TotalFee { get; set; }

  public BalanceDto NewBalance { get; set; } = null!;
}