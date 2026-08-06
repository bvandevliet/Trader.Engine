using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TraderEngine.Common.Exchanges;

namespace TraderEngine.API.Exchanges;

/// <summary>
/// Caches one authenticated <see cref="BitvavoWebSocketClient"/> per API key so repeated order
/// placements for the same user reuse a single connection instead of opening a new WebSocket per
/// order. Registered as a singleton; connections idle longer than <see cref="IdleTimeout"/> are
/// disposed lazily on the next access.
/// </summary>
public sealed class BitvavoWebSocketConnectionPool : IAsyncDisposable
{
  private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(5);

  private readonly ILoggerFactory _loggerFactory;

  private readonly ConcurrentDictionary<string, Lazy<Task<BitvavoWebSocketClient>>> _clients = new();
  private readonly ConcurrentDictionary<string, DateTimeOffset> _lastUsed = new();

  public BitvavoWebSocketConnectionPool(ILoggerFactory loggerFactory)
  {
    _loggerFactory = loggerFactory;
  }

  public async Task<BitvavoWebSocketClient> GetConnectedAsync(ExchangeCredentials credentials, CancellationToken ct)
  {
    EvictIdle();

    _lastUsed[credentials.ApiKey] = DateTimeOffset.UtcNow;

    var lazy = _clients.GetOrAdd(credentials.ApiKey, _ => new Lazy<Task<BitvavoWebSocketClient>>(
      () => ConnectNewClientAsync(credentials), LazyThreadSafetyMode.ExecutionAndPublication));

    try
    {
      return await lazy.Value;
    }
    catch
    {
      // Don't cache a failed connection attempt — the next call should retry from scratch.
      _clients.TryRemove(credentials.ApiKey, out _);
      _lastUsed.TryRemove(credentials.ApiKey, out _);
      throw;
    }
  }

  private async Task<BitvavoWebSocketClient> ConnectNewClientAsync(ExchangeCredentials credentials)
  {
    var client = new BitvavoWebSocketClient(credentials, _loggerFactory.CreateLogger<BitvavoWebSocketClient>());

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
      if (lastUsed >= cutoff || !_lastUsed.TryRemove(apiKey, out _))
        continue;

      if (_clients.TryRemove(apiKey, out var lazy) && lazy.IsValueCreated)
      {
        _ = DisposeClientAsync(lazy);
      }
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
    foreach (var apiKey in _clients.Keys.ToArray())
    {
      if (_clients.TryRemove(apiKey, out var lazy))
        await DisposeClientAsync(lazy);
    }
  }
}
