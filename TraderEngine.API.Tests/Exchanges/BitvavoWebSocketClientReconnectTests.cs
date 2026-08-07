using Microsoft.Extensions.Logging.Abstractions;
using TraderEngine.API.Exchanges;
using TraderEngine.Common.Exchanges;

namespace TraderEngine.API.Tests.Exchanges;

/// <summary>
/// Covers <see cref="BitvavoWebSocketClient"/>'s reconnect-on-unexpected-close logic: bounded
/// exponential backoff, re-authentication, re-subscribing every previously active market, giving
/// up permanently once the reconnect budget is exhausted, and consulting the shared rate-limit
/// state before attempting a reconnect. Driven entirely through <see cref="FakeWebSocketTransport"/>
/// and an injected no-op delay function, so no real socket or real waiting is involved.
/// </summary>
[TestClass]
public class BitvavoWebSocketClientReconnectTests
{
  private static readonly ExchangeCredentials _credentials = new("key", "secret");

  private static (BitvavoWebSocketClient Client, List<TimeSpan> Delays, Func<int> CreatedCount) NewClient(
    IReadOnlyList<FakeWebSocketTransport> transports, BitvavoRateLimitState? rateLimitState = null)
  {
    var index = 0;
    var delays = new List<TimeSpan>();

    Task DelayFn(TimeSpan delay, CancellationToken ct)
    {
      delays.Add(delay);
      return Task.CompletedTask;
    }

    IWebSocketTransport Factory()
    {
      var transport = transports[index];
      index++;
      return transport;
    }

    var client = new BitvavoWebSocketClient(
      _credentials,
      NullLogger<BitvavoWebSocketClient>.Instance,
      rateLimitState ?? new BitvavoRateLimitState(),
      DelayFn,
      Factory);

    return (client, delays, () => index);
  }

  private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
  {
    var deadline = DateTime.UtcNow + timeout;

    while (!condition())
    {
      if (DateTime.UtcNow > deadline)
        Assert.Fail("Condition was not met within the timeout.");

      await Task.Delay(10);
    }
  }

  [TestMethod]
  public async Task ConnectAsync_Succeeds_IsHealthyTrue_NoReconnectAttempts()
  {
    // Arrange
    var t1 = new FakeWebSocketTransport();
    var (client, delays, createdCount) = NewClient([t1]);

    // Act
    await client.ConnectAsync(CancellationToken.None);

    // Assert
    Assert.IsTrue(client.IsHealthy);
    Assert.AreEqual(0, delays.Count);
    Assert.AreEqual(1, createdCount());

    await client.DisposeAsync();
  }

  [TestMethod]
  public async Task ServerCloses_ReconnectsSuccessfully_ReauthenticatesAndResubscribes()
  {
    // Arrange
    var t1 = new FakeWebSocketTransport();
    var t2 = new FakeWebSocketTransport();
    var (client, _, _) = NewClient([t1, t2]);

    await client.ConnectAsync(CancellationToken.None);
    await client.SubscribeAccountAsync("BTC-EUR", _ => { }, CancellationToken.None);

    // Act
    t1.SimulateServerClose();

    await WaitUntilAsync(() => t2.SentMessages.Count >= 2, TimeSpan.FromSeconds(2));

    // Assert
    Assert.IsTrue(client.IsHealthy);
    Assert.IsTrue(t2.SentMessages.Any(m => m.Contains("\"action\":\"authenticate\"")));
    Assert.IsTrue(t2.SentMessages.Any(m => m.Contains("\"action\":\"subscribe\"") && m.Contains("BTC-EUR")));

    await client.DisposeAsync();
  }

  [TestMethod]
  public async Task ServerCloses_AllReconnectAttemptsFail_IsHealthyBecomesFalse_DelaysDouble()
  {
    // Arrange
    var t1 = new FakeWebSocketTransport();
    var failing = Enumerable.Range(0, 5).Select(_ => new FakeWebSocketTransport { ThrowOnConnect = true }).ToArray();
    var (client, delays, createdCount) = NewClient([t1, .. failing]);

    await client.ConnectAsync(CancellationToken.None);

    // Act
    t1.SimulateServerClose();

    await WaitUntilAsync(() => !client.IsHealthy, TimeSpan.FromSeconds(2));

    // Assert
    Assert.IsFalse(client.IsHealthy);
    Assert.AreEqual(6, createdCount()); // 1 initial + 5 failed reconnect attempts
    CollectionAssert.AreEqual(
      new[]
      {
        TimeSpan.FromMilliseconds(250),
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
      },
      delays);

    await client.DisposeAsync();
  }

  [TestMethod]
  public async Task ServerCloses_SucceedsOnThirdAttempt_StopsRetrying()
  {
    // Arrange
    var t1 = new FakeWebSocketTransport();
    var t2 = new FakeWebSocketTransport { ThrowOnConnect = true };
    var t3 = new FakeWebSocketTransport { ThrowOnConnect = true };
    var t4 = new FakeWebSocketTransport();
    var (client, delays, createdCount) = NewClient([t1, t2, t3, t4]);

    await client.ConnectAsync(CancellationToken.None);

    // Act
    t1.SimulateServerClose();

    await WaitUntilAsync(() => t4.SentMessages.Any(m => m.Contains("authenticate")), TimeSpan.FromSeconds(2));

    // Assert
    Assert.IsTrue(client.IsHealthy);
    Assert.AreEqual(2, delays.Count);
    Assert.AreEqual(4, createdCount());

    await client.DisposeAsync();
  }

  [TestMethod]
  public async Task DisposeAsync_DoesNotTriggerReconnect()
  {
    // Arrange
    var t1 = new FakeWebSocketTransport();
    var (client, _, createdCount) = NewClient([t1]);

    await client.ConnectAsync(CancellationToken.None);

    // Act
    await client.DisposeAsync();

    await Task.Delay(50); // give any errant reconnect a moment to (not) happen

    // Assert
    Assert.AreEqual(1, createdCount());
    Assert.IsFalse(client.IsHealthy);
  }

  [TestMethod]
  public async Task ServerCloses_AccountCurrentlyBanned_SkipsReconnectAttempts_GivesUpImmediately()
  {
    // Arrange
    var t1 = new FakeWebSocketTransport();
    var rateLimitState = new BitvavoRateLimitState();
    rateLimitState.ObserveBan(DateTimeOffset.UtcNow.AddSeconds(30));

    var (client, delays, createdCount) = NewClient([t1], rateLimitState);

    await client.ConnectAsync(CancellationToken.None);

    // Act
    t1.SimulateServerClose();

    await WaitUntilAsync(() => !client.IsHealthy, TimeSpan.FromSeconds(2));

    // Assert
    Assert.IsFalse(client.IsHealthy);
    Assert.AreEqual(0, delays.Count);
    Assert.AreEqual(1, createdCount()); // no reconnect attempt was even made

    await client.DisposeAsync();
  }
}
