using System.Text.Json.Serialization;
using TraderEngine.Common.Extensions;

namespace TraderEngine.Common.Enums;

[JsonConverter(typeof(EnumToStringConverter<OrderSide>))]
public enum OrderSide
{
  Buy,
  Sell,
}