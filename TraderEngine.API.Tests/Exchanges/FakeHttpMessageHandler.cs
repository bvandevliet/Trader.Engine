using System.Net;
using System.Text;

namespace TraderEngine.API.Tests.Exchanges;

/// <summary>
/// A minimal <see cref="HttpMessageHandler"/> double that always returns a canned response and
/// records the last request it handled, so HTTP-calling exchange code can be unit tested
/// without a live network dependency.
/// </summary>
internal sealed class FakeHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
{
  public HttpRequestMessage? LastRequest { get; private set; }

  public string? LastRequestBody { get; private set; }

  /// <summary>
  /// Number of requests handled so far, for asserting a caller made (or didn't make) a repeat call.
  /// </summary>
  public int RequestCount { get; private set; }

  private (string Name, string Value)[] _responseHeaders = [];

  private HttpStatusCode _statusCode = statusCode;

  private string _responseBody = responseBody;

  /// <summary>
  /// Configures response headers to attach to every subsequent canned response (e.g. Bitvavo's
  /// <c>bitvavo-ratelimit-remaining</c>/<c>bitvavo-ratelimit-resetat</c>).
  /// </summary>
  public void SetResponseHeaders(params (string Name, string Value)[] headers)
  {
    _responseHeaders = headers;
  }

  /// <summary>
  /// Reconfigures the canned response for subsequent requests, e.g. to simulate a different
  /// backend response to a second call within the same test.
  /// </summary>
  public void SetResponse(HttpStatusCode newStatusCode, string newResponseBody)
  {
    _statusCode = newStatusCode;
    _responseBody = newResponseBody;
  }

  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
  {
    RequestCount++;
    LastRequest = request;
    LastRequestBody = request.Content != null
      ? await request.Content.ReadAsStringAsync(cancellationToken)
      : null;

    var response = new HttpResponseMessage(_statusCode)
    {
      Content = new StringContent(_responseBody, Encoding.UTF8, "application/json"),
    };

    foreach (var (name, value) in _responseHeaders)
      response.Headers.TryAddWithoutValidation(name, value);

    return response;
  }
}
