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

  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
  {
    LastRequest = request;
    LastRequestBody = request.Content != null
      ? await request.Content.ReadAsStringAsync(cancellationToken)
      : null;

    return new HttpResponseMessage(statusCode)
    {
      Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
    };
  }
}
