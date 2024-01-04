using System.ComponentModel.DataAnnotations;

namespace TraderEngine.Common.DTOs.API.Request;

public class RebalanceReqDto
{
  [Required]
  public ApiCredReqDto ExchangeApiCred { get; set; } = null!;

  [Required]
  public OrderReqDto[] Orders { get; set; } = Array.Empty<OrderReqDto>();

  public RebalanceReqDto()
  {
  }

  public RebalanceReqDto(
    ApiCredReqDto exchangeApiCred,
    OrderReqDto[] orders)
  {
    ExchangeApiCred = exchangeApiCred;
    Orders = orders;
  }
}