using System.Net;

namespace TraderEngine.Web.Services;

/// <summary>
/// Wraps any non-success TraderEngine.API response other than an exchange authentication failure
/// (see <see cref="ExchangeAuthenticationException"/>) — e.g. "No recent market cap records
/// found" while ingestion is still catching up on a freshly-started stack, or a misconfigured
/// exchange name. Carries the API's own response body as the message so callers can show the
/// actual reason instead of a generic unhandled-exception page.
/// </summary>
public class TraderEngineApiException : Exception
{
  public HttpStatusCode StatusCode { get; }

  public TraderEngineApiException(HttpStatusCode statusCode, string message) : base(message)
  {
    StatusCode = statusCode;
  }
}
