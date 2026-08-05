using System.Text.Json;
using System.Text.Json.Serialization;

namespace TraderEngine.Common.Extensions;

public static class JsonSerializerExtensions
{
  public static JsonSerializerOptions ConfigureDefaultJsonSerializerOptions(this JsonSerializerOptions options, bool readOnly = false)
  {
    options.PropertyNameCaseInsensitive = true;
    options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    options.NumberHandling = JsonNumberHandling.AllowReadingFromString;
    options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.WriteIndented = false;

    if (readOnly)
      options.MakeReadOnly(true);

    return options;
  }
}
