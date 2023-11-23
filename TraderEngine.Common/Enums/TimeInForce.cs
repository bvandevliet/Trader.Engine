using System.Text.Json.Serialization;
using TraderEngine.Common.Extensions;

namespace TraderEngine.Common.Enums;

[JsonConverter(typeof(EnumToStringConverter<TimeInForce>))]
public enum TimeInForce
{
  /// <summary>
  /// Good-Til-Canceled
  /// </summary>
  GTC,
  /// <summary>
  /// Immediate-Or-Cancel
  /// </summary>
  IOC,
  /// <summary>
  /// Fill-Or-Kill
  /// </summary>
  FOK,
}