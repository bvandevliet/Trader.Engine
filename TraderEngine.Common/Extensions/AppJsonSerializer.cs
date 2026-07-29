using System.Net.Http.Json;
using System.Text.Json;

namespace TraderEngine.Common.Extensions;

/// <summary>
/// Wraps the app's shared JSON serializer options so call sites can't silently fall back to
/// System.Text.Json's own defaults by forgetting to pass options explicitly — every member here
/// bakes them in, there is no overload that omits them. Static rather than DI-injected: the
/// options are a pure, deterministic constant with no runtime/config dependency, and nothing
/// substitutes this in tests.
/// </summary>
public static class AppJsonSerializer
{
  public static JsonSerializerOptions Options { get; } =
    new JsonSerializerOptions(JsonSerializerDefaults.Web).ConfigureDefaultJsonSerializerOptions(true);

  /// <inheritdoc cref="JsonSerializer.Serialize(object?, Type, JsonSerializerOptions)"/>
  public static string Serialize(object? value, Type inputType)
  {
    return JsonSerializer.Serialize(value, inputType, Options);
  }

  /// <inheritdoc cref="JsonSerializer.Serialize{TValue}(TValue, JsonSerializerOptions)"/>
  public static string Serialize<TValue>(TValue value)
  {
    return JsonSerializer.Serialize(value, Options);
  }

  /// <inheritdoc cref="JsonSerializer.Deserialize{TValue}(string, JsonSerializerOptions)"/>
  public static T? Deserialize<T>(string json)
  {
    return JsonSerializer.Deserialize<T>(json, Options);
  }

  /// <inheritdoc cref="JsonContent.Create{TValue}(TValue, System.Net.Http.Headers.MediaTypeHeaderValue, JsonSerializerOptions)"/>
  public static JsonContent CreateContent<TValue>(TValue value)
  {
    return JsonContent.Create(value, options: Options);
  }

  /// <inheritdoc cref="HttpContentJsonExtensions.ReadFromJsonAsync{TValue}(HttpContent, JsonSerializerOptions, CancellationToken)"/>
  public static Task<T?> DeserializeAsync<T>(this HttpContent content, CancellationToken ct = default)
  {
    return content.ReadFromJsonAsync<T>(Options, ct);
  }
}
