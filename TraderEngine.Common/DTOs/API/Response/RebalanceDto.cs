namespace TraderEngine.Common.DTOs.API.Response;

public class RebalanceDto
{
  public OrderDto[] Orders { get; set; } = Array.Empty<OrderDto>();

  public decimal TotalFee => Orders.Sum(order => order.FeePaid);

  public BalanceDto NewBalance { get; set; } = null!;
}