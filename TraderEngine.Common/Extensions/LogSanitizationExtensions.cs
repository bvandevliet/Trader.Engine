namespace TraderEngine.Common.Extensions;

public static class LogSanitizationExtensions
{
  /// <summary>
  /// Strips CR/LF from a value before it's interpolated into a log message, so untrusted input
  /// (e.g. an exchange market symbol or an error string sourced from an external response) can't
  /// forge fake log entries by injecting newlines that mimic another log line.
  /// </summary>
  public static string? SanitizeForLog(this string? value)
  {
    return value?.Replace("\r", string.Empty).Replace("\n", string.Empty);
  }

  /// <inheritdoc cref="SanitizeForLog(string?)"/>
  public static string SanitizeForLog(this object value)
  {
    return value.ToString().SanitizeForLog() ?? string.Empty;
  }
}
