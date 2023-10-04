using System.Text.Json.Serialization;
using TraderEngine.Common.Extensions;

namespace TraderEngine.Common.Enums;

[JsonConverter(typeof(EnumToStringConverter<OrderStatus>))]
public enum OrderStatus
{
  BrandNew,
  New,
  Canceled,
  Filled,
  PartiallyFilled,
  Expired,
  Rejected,
}