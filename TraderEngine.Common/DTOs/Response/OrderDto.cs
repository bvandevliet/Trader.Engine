using TraderEngine.Common.Enums;

namespace TraderEngine.Common.DTOs.Response;

public class OrderDto : Request.OrderDto
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
  /// Only for orders with <see cref="Request.OrderDto.Amount"/> supplied.
  /// Amount in base currency remaining (lower than <see cref="Request.OrderDto.Amount"/> after fills).
  /// </summary>
  public decimal AmountRemaining { get; set; }

  /// <summary>
  /// Only for market orders with <see cref="Request.OrderDto.AmountQuote"/> supplied.
  /// Amount in quote currency remaining (lower than <see cref="Request.OrderDto.AmountQuote"/> after fills).
  /// </summary>
  public decimal AmountQuoteRemaining { get; set; }

  /// <summary>
  /// Amount in quote currency paid for fee.
  /// </summary>
  public decimal FeePaid { get; set; }

  /// <summary>
  /// Timestamp this order was created.
  /// </summary>
  public DateTime Created { get; set; }

  /// <summary>
  /// Timestamp this order was updated.
  /// </summary>
  public DateTime Updated { get; set; }
}