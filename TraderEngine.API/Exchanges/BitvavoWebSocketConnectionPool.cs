using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TraderEngine.Common.Exchanges;

namespace TraderEngine.API.Exchanges;

/// <summary>
/// Caches one authenticated <see cref="BitvavoWebSocketClient"/> per API key so repeated order
/// placements for the same user reuse a single connection instead of opening a new WebSocket per
/// order. Registered as a singleton.
///
/// Connections are primarily scoped to an explicit session (<see cref="AcquireSessionAsync"/>),
/// ref-counted per API key so a rebalance run's many concurrent order placements share one
/// connection kept warm for its whole duration; once the last lease for a key is released, the
/// connection is torn down after a short grace period (rather than immediately, so back-to-back
/// calls within one run don't thrash it). The older idle-timeout eviction (<see cref="EvictIdle"/>)
/// remains as an outer safety net for a leaked lease.
///
/// Once a cached client's own reconnect budget is exhausted (<see cref="BitvavoWebSocketClient.IsHealthy"/>
/// false), the API key is marked degraded (<see cref="IsSessionDegraded"/>) so subsequent calls
/// within the same session fail fast rather than repeatedly retrying a doomed connection; a new
/// session (ref count 0 to 1) clears the degraded flag for a clean slate.
/// </summary>
public sealed class BitvavoWebSocketConnectionPool : IAsyncDisposable
{
  private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(5);
  private static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(15);

  private readonly ILoggerFactory _loggerFactory;
  private readonly ILogger<BitvavoWebSocketConnectionPool> _logger;
  private readonly BitvavoRateLimitState _rateLimitState;
  private readonly Func<TimeSpan, CancellationToken, Task> _delayFn;
  private readonly Func<ExchangeCredentials, BitvavoWebSocketClient> _clientFactory;

  private readonly ConcurrentDictionary<string, Lazy<Task<BitvavoWebSocketClient>>> _clients = new();
  private readonly ConcurrentDictionary<string, DateTimeOffset> _lastUsed = new();
  private readonly ConcurrentDictionary<string, bool> _sessionDegraded = new();

  // Ref counts and pending-teardown bookkeeping are synchronous, small critical sections — a plain
  // lock is simpler and correct here (unlike BitvavoWebSocketClient's own connect/reconnect
  // sequencing, which is async and needs a SemaphoreSlim instead).
  private readonly object _sessionLock = new();
  private readonly Dictionary<string, int> _refCounts = new();
  private readonly Dictionary<string, CancellationTokenSource> _pendingTeardowns = new();

  public BitvavoWebSocketConnectionPool(
    ILoggerFactory loggerFactory,
    ILogger<BitvavoWebSocketConnectionPool> logger,
    BitvavoRateLimitState rateLimitState,
    Func<TimeSpan, CancellationToken, Task>? delayFn = null,
    Func<ExchangeCredentials, BitvavoWebSocketClient>? clientFactory = null)
  {
    _loggerFactory = loggerFactory;
    _logger = logger;
    _rateLimitState = rateLimitState;
    _delayFn = delayFn ?? Task.Delay;
    _clientFactory = clientFactory ?? (credentials => new BitvavoWebSocketClient(credentials, _loggerFactory.CreateLogger<BitvavoWebSocketClient>(), _rateLimitState));
  }

  /// <summary>
  /// True if the connection for <paramref name="apiKey"/> has exhausted its reconnect budget
  /// within the current session; callers should skip attempting <see cref="GetConnectedAsync"/>
  /// entirely and fall back to REST until a new session begins.
  /// </summary>
  public bool IsSessionDegraded(string apiKey)
  {
    return _sessionDegraded.ContainsKey(apiKey);
  }

  /// <summary>
  /// Begins a session for <paramref name="credentials"/>, eagerly warming up the connection so the
  /// first order placed in the session doesn't pay its connect+authenticate latency. Ref-counted:
  /// concurrent sessions for the same API key share one underlying connection. Disposing the
  /// returned lease releases this session's reference; the connection is torn down after a short
  /// grace period once the last reference is released, not immediately. Never throws: a failure to
  /// warm up the connection still returns a valid lease, since <see cref="BitvavoExchange.WaitForOrderEndedAsync"/>
  /// already falls back to REST per call when the connection is unavailable.
  /// </summary>
  public async Task<IAsyncDisposable> AcquireSessionAsync(ExchangeCredentials credentials, CancellationToken ct)
  {
    var apiKey = credentials.ApiKey;

    lock (_sessionLock)
    {
      if (_pendingTeardowns.Remove(apiKey, out var pendingCts))
      {
        pendingCts.Cancel();
        pendingCts.Dispose();
      }

      var newCount = _refCounts.GetValueOrDefault(apiKey) + 1;
      _refCounts[apiKey] = newCount;

      if (newCount == 1)
        _sessionDegraded.TryRemove(apiKey, out _);
    }

    try
    {
      await GetConnectedAsync(credentials, ct);
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to warm up Bitvavo WebSocket connection at session start; orders will fall back to REST as needed.");
    }

    return new SessionLease(this, apiKey);
  }

  private void ReleaseSession(string apiKey)
  {
    lock (_sessionLock)
    {
      var newCount = _refCounts.GetValueOrDefault(apiKey) - 1;

      if (newCount < 0)
      {
        _logger.LogWarning("Bitvavo WebSocket session ref count for an API key went negative; clamping to zero.");
        newCount = 0;
      }

      if (newCount > 0)
      {
        _refCounts[apiKey] = newCount;
        return;
      }

      _refCounts.Remove(apiKey);

      var cts = new CancellationTokenSource();
      _pendingTeardowns[apiKey] = cts;

      _ = TeardownAfterGraceAsync(apiKey, cts);
    }
  }

  private async Task TeardownAfterGraceAsync(string apiKey, CancellationTokenSource cts)
  {
    try
    {
      await _delayFn(GracePeriod, cts.Token);
    }
    catch (OperationCanceledException)
    {
      // A new AcquireSessionAsync cancelled this teardown; the connection is being reused.
      return;
    }

    lock (_sessionLock)
    {
      if (_pendingTeardowns.TryGetValue(apiKey, out var current) && ReferenceEquals(current, cts))
        _pendingTeardowns.Remove(apiKey);
    }

    _lastUsed.TryRemove(apiKey, out _);

    if (_clients.TryRemove(apiKey, out var lazy))
      _ = DisposeClientAsync(lazy);
  }

  public async Task<BitvavoWebSocketClient> GetConnectedAsync(ExchangeCredentials credentials, CancellationToken ct)
  {
    var apiKey = credentials.ApiKey;

    if (IsSessionDegraded(apiKey))
      throw new InvalidOperationException("Bitvavo WebSocket is degraded for the current session; use REST instead.");

    EvictIdle();

    _lastUsed[apiKey] = DateTimeOffset.UtcNow;

    var lazy = _clients.GetOrAdd(apiKey, _ => new Lazy<Task<BitvavoWebSocketClient>>(
      () => ConnectNewClientAsync(credentials), LazyThreadSafetyMode.ExecutionAndPublication));

    BitvavoWebSocketClient client;
    try
    {
      client = await lazy.Value;
    }
    catch
    {
      // Don't cache a failed connection attempt — the next call should retry from scratch.
      _clients.TryRemove(apiKey, out _);
      _lastUsed.TryRemove(apiKey, out _);
      throw;
    }

    if (!client.IsHealthy)
    {
      // The cached connection has exhausted its own reconnect budget. Mark the session degraded
      // so subsequent calls short-circuit above instead of each paying for a fresh, likely-doomed
      // connection attempt against whatever is still preventing this one from working.
      _sessionDegraded[apiKey] = true;
      _clients.TryRemove(apiKey, out _);

      throw new InvalidOperationException("Bitvavo WebSocket connection is unhealthy.");
    }

    return client;
  }

  private async Task<BitvavoWebSocketClient> ConnectNewClientAsync(ExchangeCredentials credentials)
  {
    var client = _clientFactory(credentials);

    // Connection lifetime is owned by the pool, not by whichever caller happened to trigger it —
    // don't tie it to that caller's cancellation token.
    await client.ConnectAsync(CancellationToken.None);

    return client;
  }

  private void EvictIdle()
  {
    var cutoff = DateTimeOffset.UtcNow - IdleTimeout;

    foreach (var (apiKey, lastUsed) in _lastUsed)
    {
      if (lastUsed >= cutoff)
        continue;

      lock (_sessionLock)
      {
        if (_refCounts.GetValueOrDefault(apiKey) > 0)
          continue; // actively leased — the grace-period teardown mechanism owns this key's lifecycle
      }

      if (!_lastUsed.TryRemove(apiKey, out _))
        continue;

      if (_clients.TryRemove(apiKey, out var lazy) && lazy.IsValueCreated)
        _ = DisposeClientAsync(lazy);
    }
  }

  private static async Task DisposeClientAsync(Lazy<Task<BitvavoWebSocketClient>> lazy)
  {
    try
    {
      var client = await lazy.Value;
      await client.DisposeAsync();
    }
    catch
    {
      // Connection never succeeded or is already broken — nothing to dispose.
    }
  }

  public async ValueTask DisposeAsync()
  {
    foreach (var cts in _pendingTeardowns.Values)
      cts.Cancel();

    foreach (var apiKey in _clients.Keys.ToArray())
    {
      if (_clients.TryRemove(apiKey, out var lazy))
        await DisposeClientAsync(lazy);
    }
  }

  private sealed class SessionLease : IAsyncDisposable
  {
    private readonly BitvavoWebSocketConnectionPool _pool;
    private readonly string _apiKey;
    private int _disposed;

    public SessionLease(BitvavoWebSocketConnectionPool pool, string apiKey)
    {
      _pool = pool;
      _apiKey = apiKey;
    }

    public ValueTask DisposeAsync()
    {
      if (Interlocked.Exchange(ref _disposed, 1) == 0)
        _pool.ReleaseSession(_apiKey);

      return ValueTask.CompletedTask;
    }
  }
}
