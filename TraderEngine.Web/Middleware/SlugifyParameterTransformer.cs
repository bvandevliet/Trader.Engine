using System.Text.Json;

namespace TraderEngine.Web.Middleware;

internal class SlugifyParameterTransformer : IOutboundParameterTransformer
{
  private static readonly JsonNamingPolicy _slugifyNamingPolicy = JsonNamingPolicy.KebabCaseLower;

  public string? TransformOutbound(object? value)
  {
    return _slugifyNamingPolicy.ConvertName(value?.ToString() ?? string.Empty);
  }
}
