using System.ComponentModel.DataAnnotations;
using TraderEngine.Common.Enums;

namespace TraderEngine.Common.DTOs.Request;

public class OrderDto
{
  /// <summary>
  /// The market to trade.
  /// </summary>
  [Required]
  public MarketDto Market { get; set; } = null!;

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
  public decimal? Price { get; set; }

  /// <summary>
  /// For limit orders, optionally for market orders. Specifies the amount of base currency that will be bought/sold.
  /// If specified for market order, it gets priority over <see cref="AmountQuote"/>.
  /// </summary>
  public decimal? Amount { get; set; }

  /// <summary>
  /// Only for market orders. If <see cref="Amount"/> is not specified,
  /// this <see cref="AmountQuote"/> of the quote currency will be bought/sold for the best price available.
  /// </summary>
  public decimal? AmountQuote { get; set; }

  /// <summary>
  /// Only for limit orders. Determines how long orders remain active.
  /// </summary>
  public TimeInForce TimeInForce { get; set; } = TimeInForce.GTC;

  /// <summary>
  /// Expected amount in quote currency to be paid for fee.
  /// </summary>
  public decimal FeeExpected { get; set; }

  public OrderDto()
  {
  }

  /// <param name="market"><inheritdoc cref="Market"/></param>
  /// <param name="side"><inheritdoc cref="Side"/></param>
  /// <param name="type"><inheritdoc cref="Type"/></param>
  public OrderDto(
    MarketDto market,
    OrderSide side,
    OrderType type)
  {
    Market = market;
    Side = side;
    Type = type;
  }
}