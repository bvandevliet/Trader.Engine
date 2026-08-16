using System.Text.RegularExpressions;

namespace TraderEngine.Web.Extensions;

public static partial class UserInputSanitizationExtensions
{
  private const int MaxSearchFilterLength = 200;

  /// <summary>
  /// Strips HTML-significant characters (<c>&lt; &gt; " ' &amp;</c>) and caps the length of a
  /// free-text search filter that gets round-tripped back into the page (e.g. as a route value
  /// or form field), so it can never be used to break out of an HTML attribute/context even if a
  /// Razor Tag Helper's own output encoding were ever bypassed.
  /// </summary>
  public static string? SanitizeSearchFilter(this string? value)
  {
    if (string.IsNullOrEmpty(value))
    {
      return value;
    }

    var truncated = value.Length > MaxSearchFilterLength ? value[..MaxSearchFilterLength] : value;

    return HtmlUnsafeCharacters().Replace(truncated, string.Empty);
  }

  [GeneratedRegex("[<>\"'&]")]
  private static partial Regex HtmlUnsafeCharacters();
}
