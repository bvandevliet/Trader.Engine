using TraderEngine.Common.DTOs.Request;

namespace TraderEngine.API.DTOs.Bitvavo.Response;

public class BitvavoOrderDto
{
  /// <summary>
  /// The unique order ID for this market. ID's are guaranteed to be unique per market, but not over all markets.
  /// </summary>
  public string OrderId { get; set; } = null!;

  /// <summary>
  /// The market being traded.
  /// </summary>
  public string Market { get; set; } = null!;

  /// <summary>
  /// Current status of the order.
  /// Enum: "new" "awaitingTrigger" "canceled" "canceledAuction" "canceledSelfTradePrevention"
  /// "canceledIOC" "canceledFOK" "canceledMarketProtection" "canceledPostOnly"
  /// "filled" "partiallyFilled" "expired" "rejected".
  /// </summary>
  public string Status { get; set; } = null!;

  /// <summary>
  /// Enum: "buy" "sell".
  /// </summary>
  public string Side { get; set; } = null!;

  /// <summary>
  /// Enum: "market" "limit" "stopLoss" "stopLossLimit" "takeProfit" "takeProfitLimit".
  /// </summary>
  public string OrderType { get; set; } = null!;

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
  /// Amount in base currency filled.
  /// </summary>
  public string FilledAmount { get; set; } = null!;

  /// <summary>
  /// Amount in quote currency filled.
  /// </summary>
  public string FilledAmountQuote { get; set; } = null!;

  /// <summary>
  /// Only for orders with <see cref="OrderReqDto.Amount"/> supplied.
  /// Amount in base currency remaining (lower than <see cref="OrderReqDto.Amount"/> after fills).
  /// </summary>
  public string? AmountRemaining { get; set; }

  /// <summary>
  /// Only for market orders with <see cref="OrderReqDto.AmountQuote"/> supplied.
  /// Amount in quote currency remaining (lower than <see cref="OrderReqDto.AmountQuote"/> after fills).
  /// </summary>
  public string? AmountQuoteRemaining { get; set; }

  /// <summary>
  /// Amount in quote currency paid for fee.
  /// </summary>
  public string FeePaid { get; set; } = null!;

  /// <summary>
  /// Timestamp this order was created.
  /// </summary>
  public DateTime Created { get; set; }

  /// <summary>
  /// Timestamp this order was updated.
  /// </summary>
  public DateTime Updated { get; set; }
}