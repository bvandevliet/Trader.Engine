using System.ComponentModel.DataAnnotations;

namespace TraderEngine.Common.DTOs.API.Request;

public class ExecuteOrdersReqDto
{
  [Required]
  public ApiCredReqDto ExchangeApiCred { get; set; } = null!;

  [Required]
  public OrderReqDto[] Orders { get; set; } = Array.Empty<OrderReqDto>();

  public ExecuteOrdersReqDto()
  {
  }

  public ExecuteOrdersReqDto(
    ApiCredReqDto exchangeApiCred,
    OrderReqDto[] orders)
  {
    ExchangeApiCred = exchangeApiCred;
    Orders = orders;
  }
}