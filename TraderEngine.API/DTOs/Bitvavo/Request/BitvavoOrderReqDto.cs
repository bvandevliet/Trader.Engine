using System.ComponentModel.DataAnnotations;

namespace TraderEngine.API.DTOs.Bitvavo.Request;

public class BitvavoOrderReqDto
{
  /// <summary>
  /// Should be all capital letters with a dash sign in the middle. Example: BTC-EUR.
  /// </summary>
  [Required]
  public string Market { get; set; } = null!;

  /// <summary>
  /// Enum: "buy" "sell".
  /// When placing a buy order the base currency will be bought for the quote currency.
  /// When placing a sell order the base currency will be sold for the quote currency.
  /// </summary>
  [Required]
  public string Side { get; set; } = null!;

  /// <summary>
  /// Your identifier for the trader or the bot within your account that made the request.
  /// </summary>
  [Required]
  public int OperatorId { get; set; }

  /// <summary>
  /// Enum: "market" "limit" "stopLoss" "stopLossLimit" "takeProfit" "takeProfitLimit".
  /// For limit orders, amount and price are required.
  /// For market orders either amount or amountQuote is required.
  /// </summary>
  public string OrderType { get; set; } = "market";

  /// <summary>
  /// Only for limit orders.
  /// Specifies the amount in quote currency that is paid/received for each unit of base currency.
  /// </summary>
  public string? Price { get; set; }

  /// <summary>
  /// Specifies the amount of the base asset that will be bought/sold.
  /// </summary>
  public string? Amount { get; set; }

  /// <summary>
  /// Only for market orders.
  /// If amountQuote is specified, [amountQuote] of the quote currency will be bought/sold for the best price available.
  /// </summary>
  public string? AmountQuote { get; set; }

  /// <summary>
  /// Enum: "GTC" "IOC" "FOK". Default: "GTC".
  /// Only for limit orders: Determines how long orders remain active.
  /// Possible values: Good-Til-Canceled (GTC), Immediate-Or-Cancel (IOC), Fill-Or-Kill (FOK). GTC orders will remain on the order book until they are filled or canceled. IOC orders will fill against existing orders, but will cancel any remaining amount after that.
  /// FOK orders will fill against existing orders in its entirety or will be canceled (if the entire order cannot be filled).
  /// </summary>
  public string? TimeInForce { get; set; }

  /// <summary>
  /// Only for market orders.
  /// In order to protect clients from filling market orders with undesirable prices,
  /// the remainder of market orders will be canceled once the next fill price is 10% worse than the best fill price (best bid/ask at first match).
  /// If you wish to disable this protection, set this value to ‘true’.
  /// </summary>
  public bool? DisableMarketProtection { get; set; }

  /// <summary>
  /// If this is set to 'true', all order information is returned.
  /// Set this to 'false' when only an acknowledgement of success or failure is required, this is faster.
  /// </summary>
  public bool ResponseRequired { get; set; } = false;
}