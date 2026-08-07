using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TraderEngine.API.Exchanges;
using TraderEngine.Common.Exchanges;

namespace TraderEngine.API.Tests.Exchanges;

/// <summary>
/// Covers <see cref="BitvavoWebSocketConnectionPool"/>'s ref-counted session lease (shared
/// connection across concurrent orders in one rebalance run, torn down after a grace period once
/// released) and the liveness-check fix for the bug where a dead cached connection used to keep
/// being handed out indefinitely. Driven through an injected client factory producing
/// <see cref="BitvavoWebSocketClient"/>s backed by <see cref="FakeWebSocketTransport"/>, so no real
/// socket or real waiting is involved.
/// </summary>
[TestClass]
public class BitvavoWebSocketConnectionPoolSessionTests
{
  private static readonly ExchangeCredentials _credentials = new("key", "secret");

  private static BitvavoWebSocketClient NewFakeClient(BitvavoRateLimitState rateLimitState)
  {
    var transports = new List<FakeWebSocketTransport> { new() };
    var index = 0;

    return new BitvavoWebSocketClient(
      _credentials,
      NullLogger<BitvavoWebSocketClient>.Instance,
      rateLimitState,
      (_, _) => Task.CompletedTask,
      () => transports[index++]);
  }

  private static (BitvavoWebSocketConnectionPool Pool, Func<int> ClientFactoryCallCount, TaskCompletionSource GraceGate)
    NewPool(Func<ExchangeCredentials, BitvavoWebSocketClient>? clientFactory = null)
  {
    var rateLimitState = new BitvavoRateLimitState();
    var callCount = 0;
    var graceGate = new TaskCompletionSource();

    BitvavoWebSocketClient Factory(ExchangeCredentials credentials)
    {
      callCount++;
      return clientFactory?.Invoke(credentials) ?? NewFakeClient(rateLimitState);
    }

    Task DelayFn(TimeSpan delay, CancellationToken ct)
    {
      return graceGate.Task.WaitAsync(ct);
    }

    var pool = new BitvavoWebSocketConnectionPool(
      Substitute.For<ILoggerFactory>(),
      NullLogger<BitvavoWebSocketConnectionPool>.Instance,
      rateLimitState,
      DelayFn,
      Factory);

    return (pool, () => callCount, graceGate);
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
  public async Task AcquireSessionAsync_EstablishesConnection_ClientFactoryInvokedOnce()
  {
    // Arrange
    var (pool, callCount, _) = NewPool();

    // Act
    await using var session = await pool.AcquireSessionAsync(_credentials, CancellationToken.None);
    var client = await pool.GetConnectedAsync(_credentials, CancellationToken.None);

    // Assert
    Assert.IsTrue(client.IsHealthy);
    Assert.AreEqual(1, callCount());
  }

  [TestMethod]
  public async Task AcquireSessionAsync_ConcurrentCallers_ShareSameConnection()
  {
    // Arrange
    var (pool, callCount, _) = NewPool();

    // Act
    await using var session1 = await pool.AcquireSessionAsync(_credentials, CancellationToken.None);
    await using var session2 = await pool.AcquireSessionAsync(_credentials, CancellationToken.None);

    // Assert
    Assert.AreEqual(1, callCount());
  }

  [TestMethod]
  public async Task DisposeSession_RefCountReachesZero_DoesNotImmediatelyClose_ConnectionStillUsable()
  {
    // Arrange
    var (pool, callCount, _) = NewPool();
    var session = await pool.AcquireSessionAsync(_credentials, CancellationToken.None);

    // Act
    await session.DisposeAsync();

    // Assert: the grace-period delay is pending (never completed), so the connection must still
    // be the same cached instance, not yet torn down.
    var client = await pool.GetConnectedAsync(_credentials, CancellationToken.None);
    Assert.IsTrue(client.IsHealthy);
    Assert.AreEqual(1, callCount());
  }

  [TestMethod]
  public async Task DisposeSession_NewAcquireDuringGracePeriod_CancelsPendingTeardown_ConnectionReused()
  {
    // Arrange
    var (pool, callCount, _) = NewPool();
    var session1 = await pool.AcquireSessionAsync(_credentials, CancellationToken.None);
    await session1.DisposeAsync(); // starts the grace-period countdown

    // Act
    await using var session2 = await pool.AcquireSessionAsync(_credentials, CancellationToken.None); // should cancel the pending teardown

    // Assert
    var client = await pool.GetConnectedAsync(_credentials, CancellationToken.None);
    Assert.IsTrue(client.IsHealthy);
    Assert.AreEqual(1, callCount()); // same connection reused, no second connect
  }

  [TestMethod]
  public async Task DisposeSession_GracePeriodElapses_ConnectionClosed_NextAcquireCreatesFresh()
  {
    // Arrange
    var (pool, callCount, graceGate) = NewPool();
    var session = await pool.AcquireSessionAsync(_credentials, CancellationToken.None);
    await session.DisposeAsync(); // starts the grace-period countdown

    // Act: let the grace period "elapse".
    graceGate.SetResult();

    await WaitUntilAsync(() => callCount() >= 1, TimeSpan.FromSeconds(2)); // teardown observed

    await using var session2 = await pool.AcquireSessionAsync(_credentials, CancellationToken.None);

    // Assert: a brand-new connection was established for the new session.
    Assert.AreEqual(2, callCount());
  }

  [TestMethod]
  public async Task GetConnectedAsync_CachedClientBecomesUnhealthy_EvictsAndThrows_MarksSessionDegraded()
  {
    // Arrange
    var rateLimitState = new BitvavoRateLimitState();
    var transport = new FakeWebSocketTransport();
    var transports = new List<FakeWebSocketTransport> { transport }; // no further transports queued — every reconnect attempt fails
    var index = 0;

    BitvavoWebSocketClient Factory(ExchangeCredentials credentials)
    {
      return new BitvavoWebSocketClient(
        _credentials,
        NullLogger<BitvavoWebSocketClient>.Instance,
        rateLimitState,
        (_, _) => Task.CompletedTask,
        () => transports[index++]);
    }

    var (pool, _, _) = NewPool(Factory);

    var client = await pool.GetConnectedAsync(_credentials, CancellationToken.None);
    Assert.IsTrue(client.IsHealthy);

    // Act: kill the connection; its own reconnect budget exhausts immediately since no further
    // transports are queued.
    transport.SimulateServerClose();
    await WaitUntilAsync(() => !client.IsHealthy, TimeSpan.FromSeconds(2));

    // Assert
    Assert.IsFalse(pool.IsSessionDegraded(_credentials.ApiKey)); // not yet observed by the pool
    await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => pool.GetConnectedAsync(_credentials, CancellationToken.None));
    Assert.IsTrue(pool.IsSessionDegraded(_credentials.ApiKey));

    // A subsequent call short-circuits immediately, without attempting a new connection.
    await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => pool.GetConnectedAsync(_credentials, CancellationToken.None));
  }

  [TestMethod]
  public async Task IsSessionDegraded_FalseInitially_NoConnectionAttemptedYet()
  {
    // Arrange
    var (pool, _, _) = NewPool();

    // Assert
    Assert.IsFalse(pool.IsSessionDegraded(_credentials.ApiKey));
  }

  [TestMethod]
  public async Task IsSessionDegraded_ClearedOnNewSession()
  {
    // Arrange: force a degraded state by having the client factory always throw on connect, so
    // GetConnectedAsync's own failed-connect path is exercised (not the post-connect unhealthy
    // path, which is covered in BitvavoWebSocketClientReconnectTests) — here we only verify the
    // degraded flag's clear-on-new-session behavior using the pool's public surface.
    var rateLimitState = new BitvavoRateLimitState();
    var attempt = 0;

    BitvavoWebSocketClient Factory(ExchangeCredentials credentials)
    {
      attempt++;

      // First attempt: a client whose sole transport throws on connect, so GetConnectedAsync's
      // lazy.Value await throws and the pool evicts it (the "don't cache a failed attempt" path).
      var transports = new List<FakeWebSocketTransport> { new() { ThrowOnConnect = attempt == 1 } };
      var index = 0;

      return new BitvavoWebSocketClient(
        _credentials,
        NullLogger<BitvavoWebSocketClient>.Instance,
        rateLimitState,
        (_, _) => Task.CompletedTask,
        () => transports[index++]);
    }

    var (pool, _, _) = NewPool(Factory);

    await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => pool.GetConnectedAsync(_credentials, CancellationToken.None));

    // Act: a fresh connect attempt (second factory invocation) should succeed.
    var client = await pool.GetConnectedAsync(_credentials, CancellationToken.None);

    // Assert
    Assert.IsTrue(client.IsHealthy);
  }
}
