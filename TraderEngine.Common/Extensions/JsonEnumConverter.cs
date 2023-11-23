using System.Text.Json;
using System.Text.Json.Serialization;

namespace TraderEngine.Common.Extensions;

public abstract class JsonEnumConverterBase<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
{
  public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    if (reader.TokenType == JsonTokenType.String)
    {
      string? enumValueString = reader.GetString();

      if (Enum.TryParse(enumValueString, ignoreCase: true, out TEnum enumValue))
      {
        return enumValue;
      }
    }
    else if (reader.TokenType == JsonTokenType.Number)
    {
      if (reader.TryGetInt32(out int enumValue))
      {
        return Enum.IsDefined(typeof(TEnum), enumValue)
            ? (TEnum)Enum.ToObject(typeof(TEnum), enumValue)
            : Enum.IsDefined(typeof(TEnum), -1)
            ? (TEnum)Enum.ToObject(typeof(TEnum), -1)
            : default;
      }
    }

    throw new JsonException($"Unable to convert '{reader.GetByte()}' to enum type '{typeof(TEnum)}'.");
  }
}

public class EnumToStringConverter<TEnum> : JsonEnumConverterBase<TEnum> where TEnum : struct, Enum
{
  public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
  {
    writer.WriteStringValue(value.ToString());
  }
}

public class EnumToIntegerConverter<TEnum> : JsonEnumConverterBase<TEnum> where TEnum : struct, Enum
{
  public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
  {
    writer.WriteNumberValue(Convert.ToInt32(value));
  }
}
