using System.Text.Json.Serialization;
using TraderEngine.Common.Extensions;

namespace TraderEngine.Common.Enums;

[JsonConverter(typeof(EnumToStringConverter<MarketStatus>))]
public enum MarketStatus
{
  Unavailable,
  Trading,
  Halted,
  Auction,
}