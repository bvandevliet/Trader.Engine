namespace TraderEngine.Common.DTOs.API.Response;

public class RebalanceDto
{
  public List<OrderDto> Orders { get; set; } = new();

  public BalanceDto NewBalance { get; set; } = null!;
}