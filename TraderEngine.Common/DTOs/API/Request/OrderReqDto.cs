using System.ComponentModel.DataAnnotations;
using TraderEngine.Common.Enums;

namespace TraderEngine.Common.DTOs.API.Request;

public class OrderReqDto
{
  /// <summary>
  /// The market to trade.
  /// </summary>
  [Required]
  public MarketReqDto Market { get; set; } = null!;

  /// <summary>
  /// When placing a buy order the base currency will be bought for the quote currency.
  /// When placing a sell order the base currency will be sold for the quote currency.
  /// </summary>
  [Required]
  public OrderSide Side { get; set; }

  /// <summary>
  /// For limit orders, <see cref="Amount"/> and <see cref="Price"/> are required.
  /// For market orders either <see cref="Amount"/> or <see cref="AmountQuote"/> is required.
  /// </summary>
  [Required]
  public OrderType Type { get; set; }

  /// <summary>
  /// Only for limit orders.
  /// Specifies the amount in quote currency that is paid/received for each unit of base currency.
  /// </summary>
  // TODO: Require either (Amount and Price) or (AmountQuote) !!
  public decimal? Price { get; set; }

  /// <summary>
  /// For limit orders, optionally for market orders. Specifies the amount of base currency that will be bought/sold.
  /// If specified for market order, it gets priority over <see cref="AmountQuote"/>.
  /// </summary>
  // TODO: Require either (Amount and Price) or (AmountQuote) !!
  public decimal? Amount { get; set; }

  /// <summary>
  /// Only for market orders. If <see cref="Amount"/> is not specified,
  /// this <see cref="AmountQuote"/> of the quote currency will be bought/sold for the best price available.
  /// </summary>
  // TODO: Require either (Amount and Price) or (AmountQuote) !!
  public decimal? AmountQuote { get; set; }

  /// <summary>
  /// Only for limit orders. Determines how long orders remain active.
  /// </summary>
  public TimeInForce TimeInForce { get; set; } = TimeInForce.GTC;
}