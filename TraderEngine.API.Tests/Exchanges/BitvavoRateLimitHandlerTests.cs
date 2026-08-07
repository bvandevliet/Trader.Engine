using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using TraderEngine.API.Exchanges;

namespace TraderEngine.API.Tests.Exchanges;

/// <summary>
/// Covers <see cref="BitvavoRateLimitHandler"/>'s proactive throttling and errorCode-105 fallback
/// backoff, driven through an <see cref="HttpMessageInvoker"/> wrapping the handler under test with
/// a scriptable fake inner handler, so no real network or real waiting is involved.
/// </summary>
[TestClass]
public class BitvavoRateLimitHandlerTests
{
  private static (BitvavoRateLimitHandler Handler, BitvavoRateLimitState State, List<TimeSpan> Delays) NewHandler(
    FakeHttpMessageHandler inner)
  {
    var state = new BitvavoRateLimitState();
    var delays = new List<TimeSpan>();

    Task DelayFn(TimeSpan delay, CancellationToken ct)
    {
      delays.Add(delay);
      return Task.CompletedTask;
    }

    var handler = new BitvavoRateLimitHandler(state, NullLogger<BitvavoRateLimitHandler>.Instance, DelayFn)
    {
      InnerHandler = inner,
    };

    return (handler, state, delays);
  }

  private static Task<HttpResponseMessage> SendAsync(BitvavoRateLimitHandler handler, string url = "https://api.bitvavo.com/v2/time")
  {
    using var invoker = new HttpMessageInvoker(handler, disposeHandler: false);

    return invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, url), CancellationToken.None);
  }

  [TestMethod]
  public async Task ObserveHeaders_ParsesValidRemainingAndResetAt_UpdatesState()
  {
    // Arrange
    var resetAt = DateTimeOffset.UtcNow.AddSeconds(30);
    var inner = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
    inner.SetResponseHeaders(("bitvavo-ratelimit-remaining", "900"), ("bitvavo-ratelimit-resetat", resetAt.ToUnixTimeMilliseconds().ToString()));
    var (handler, state, _) = NewHandler(inner);

    // Act
    _ = await SendAsync(handler);

    // Assert
    Assert.AreEqual(900, state.Remaining);
    Assert.AreEqual(resetAt.ToUnixTimeMilliseconds(), state.ResetAt!.Value.ToUnixTimeMilliseconds());
  }

  [TestMethod]
  public async Task ObserveHeaders_MissingHeaders_DoesNotThrow_StateUnchanged()
  {
    // Arrange
    var inner = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
    var (handler, state, _) = NewHandler(inner);

    // Act
    _ = await SendAsync(handler);

    // Assert
    Assert.AreEqual(-1, state.Remaining);
    Assert.IsNull(state.ResetAt);
  }

  [TestMethod]
  public async Task ObserveHeaders_MalformedHeaderValue_IsIgnored()
  {
    // Arrange
    var inner = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
    inner.SetResponseHeaders(("bitvavo-ratelimit-remaining", "not-a-number"), ("bitvavo-ratelimit-resetat", "123"));
    var (handler, state, _) = NewHandler(inner);

    // Act
    _ = await SendAsync(handler);

    // Assert
    Assert.AreEqual(-1, state.Remaining);
  }

  [TestMethod]
  public async Task SendAsync_RemainingUnknown_DoesNotThrottle()
  {
    // Arrange
    var inner = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
    var (handler, _, delays) = NewHandler(inner);

    // Act
    _ = await SendAsync(handler);

    // Assert
    Assert.AreEqual(0, delays.Count);
  }

  [TestMethod]
  public async Task SendAsync_RemainingBelowThreshold_WithFutureResetAt_WaitsUntilResetAt()
  {
    // Arrange
    var inner = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
    var (handler, state, delays) = NewHandler(inner);
    var resetAt = DateTimeOffset.UtcNow.AddSeconds(10);
    state.ObserveHeaders(10, resetAt.ToUnixTimeMilliseconds());

    // Act
    _ = await SendAsync(handler);

    // Assert
    Assert.AreEqual(1, delays.Count);
    Assert.IsTrue(delays[0] > TimeSpan.FromSeconds(9) && delays[0] <= TimeSpan.FromSeconds(10));
  }

  [TestMethod]
  public async Task SendAsync_RemainingBelowThreshold_ResetAtInPast_DoesNotWait()
  {
    // Arrange
    var inner = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
    var (handler, state, delays) = NewHandler(inner);
    state.ObserveHeaders(10, DateTimeOffset.UtcNow.AddSeconds(-5).ToUnixTimeMilliseconds());

    // Act
    _ = await SendAsync(handler);

    // Assert
    Assert.AreEqual(0, delays.Count);
  }

  [TestMethod]
  public async Task SendAsync_WaitDuration_IsCappedAt65Seconds()
  {
    // Arrange
    var inner = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
    var (handler, state, delays) = NewHandler(inner);
    state.ObserveHeaders(0, DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds());

    // Act
    _ = await SendAsync(handler);

    // Assert
    Assert.AreEqual(1, delays.Count);
    Assert.IsTrue(delays[0] <= TimeSpan.FromSeconds(65));
  }

  [TestMethod]
  public async Task SendAsync_ErrorResponseWithErrorCode105_TriggersConservativeBackoff_WhenHeadersAbsent()
  {
    // Arrange
    var inner = new FakeHttpMessageHandler(HttpStatusCode.TooManyRequests, """{"errorCode":105,"error":"Rate limit exceeded."}""");
    var (handler, state, _) = NewHandler(inner);
    var before = DateTimeOffset.UtcNow;

    // Act
    _ = await SendAsync(handler);

    // Assert
    Assert.AreEqual(0, state.Remaining);
    Assert.IsTrue(state.ResetAt >= before.AddSeconds(59) && state.ResetAt <= before.AddSeconds(61));
  }

  [TestMethod]
  public async Task SendAsync_ErrorResponseWithErrorCode105_HeadersPresentAndUsable_HeadersWinOverBodyFallback()
  {
    // Arrange
    var resetAt = DateTimeOffset.UtcNow.AddSeconds(5);
    var inner = new FakeHttpMessageHandler(HttpStatusCode.TooManyRequests, """{"errorCode":105,"error":"Rate limit exceeded."}""");
    inner.SetResponseHeaders(("bitvavo-ratelimit-remaining", "0"), ("bitvavo-ratelimit-resetat", resetAt.ToUnixTimeMilliseconds().ToString()));
    var (handler, state, _) = NewHandler(inner);

    // Act
    _ = await SendAsync(handler);

    // Assert: the header-derived resetAt (5s out), not the 60s fixed fallback, is what's stored.
    Assert.AreEqual(resetAt.ToUnixTimeMilliseconds(), state.ResetAt!.Value.ToUnixTimeMilliseconds());
  }

  [TestMethod]
  public async Task SendAsync_ErrorResponseWithoutErrorCode105_DoesNotTriggerBackoff()
  {
    // Arrange
    var inner = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, """{"errorCode":999,"error":"Unexpected."}""");
    var (handler, state, _) = NewHandler(inner);

    // Act
    _ = await SendAsync(handler);

    // Assert
    Assert.AreEqual(-1, state.Remaining);
    Assert.IsNull(state.ResetAt);
  }

  [TestMethod]
  public async Task SendAsync_ResponseBodyStillReadableAfterHandler()
  {
    // Arrange
    const string body = """{"errorCode":105,"error":"Rate limit exceeded."}""";
    var inner = new FakeHttpMessageHandler(HttpStatusCode.TooManyRequests, body);
    var (handler, _, _) = NewHandler(inner);

    // Act
    var response = await SendAsync(handler);

    // Assert
    Assert.AreEqual(body, await response.Content.ReadAsStringAsync());
  }
}
