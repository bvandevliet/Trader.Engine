using System.Text.Json;
using System.Text.Json.Serialization;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.Enums;

namespace TraderEngine.Common.DTOs.API.Response;

public class OrderDto : OrderReqDto
{
  /// <summary>
  /// Order Id.
  /// </summary>
  public string? Id { get; set; }

  /// <summary>
  /// Current status of the order.
  /// </summary>
  public OrderStatus Status { get; set; }

  /// <summary>
  /// Amount in base currency filled.
  /// </summary>
  public decimal AmountFilled { get; set; }

  /// <summary>
  /// Amount in quote currency filled.
  /// </summary>
  public decimal AmountQuoteFilled { get; set; }

  /// <summary>
  /// Only for orders with <see cref="OrderReqDto.Amount"/> supplied.
  /// Amount in base currency remaining (lower than <see cref="OrderReqDto.Amount"/> after fills).
  /// </summary>
  public decimal AmountRemaining { get; set; }

  /// <summary>
  /// Only for market orders with <see cref="OrderReqDto.AmountQuote"/> supplied.
  /// Amount in quote currency remaining (lower than <see cref="OrderReqDto.AmountQuote"/> after fills).
  /// </summary>
  public decimal AmountQuoteRemaining { get; set; }

  /// <summary>
  /// Amount in quote currency paid for fee.
  /// </summary>
  public decimal FeePaid { get; set; }

  /// <summary>
  /// Timestamp this order was created.
  /// </summary>
  // TODO: MAKE THIS WORK, EVEN THOUGH WE DON'T NEED IT.
  //public DateTime? Created { get; set; }

  /// <summary>
  /// Timestamp this order was updated.
  /// </summary>
  // TODO: MAKE THIS WORK, EVEN THOUGH WE DON'T NEED IT.
  //public DateTime? Updated { get; set; }

  [JsonIgnore]
  public bool HasEnded => Status is not OrderStatus.BrandNew and not OrderStatus.New and not OrderStatus.PartiallyFilled;

  public override string ToString() => JsonSerializer.Serialize(this, new JsonSerializerOptions() { WriteIndented = true });
}