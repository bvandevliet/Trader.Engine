namespace TraderEngine.Web.Services;

/// <summary>
/// Thrown whenever a request can't proceed because the current user's exchange API credentials
/// are either not configured yet or were rejected by the exchange (TraderEngine.API responding
/// 401) — lets every call site show a specific, actionable message instead of letting an
/// <see cref="HttpRequestException"/> bubble up as an unhandled, generic error page.
/// </summary>
public class ExchangeAuthenticationException : Exception
{
  public ExchangeAuthenticationException(string message) : base(message)
  {
  }
}
