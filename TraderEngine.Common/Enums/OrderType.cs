using System.Text.Json.Serialization;
using TraderEngine.Common.Extensions;

namespace TraderEngine.Common.Enums;

[JsonConverter(typeof(EnumToStringConverter<OrderType>))]
public enum OrderType
{
  Market,
  Limit,
  //StopLoss,
  //StopLossLimit,
  //TakeProfit,
  //TakeProfitLimit,
}