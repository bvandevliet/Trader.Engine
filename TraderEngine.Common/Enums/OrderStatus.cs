using System.Text.Json.Serialization;
using TraderEngine.Common.Extensions;

namespace TraderEngine.Common.Enums;

[JsonConverter(typeof(EnumToStringConverter<OrderStatus>))]
public enum OrderStatus
{
  BrandNew,
  New,
  PartiallyFilled,
  Filled,
  Canceled,
  Expired,
  Failed,
  Rejected,
}