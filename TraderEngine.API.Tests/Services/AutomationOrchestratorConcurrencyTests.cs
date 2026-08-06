using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TraderEngine.API.Factories;
using TraderEngine.API.Services;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Exchanges;
using TraderEngine.Common.Models;
using TraderEngine.Common.Results;
using TraderEngine.Common.Services;
using TraderEngine.Data.Repositories;

namespace TraderEngine.API.Tests.Services;

/// <summary>
/// Guards against a critical class of bug: <see cref="AutomationOrchestrator.RunAsync"/>
/// processes every user's automation concurrently via <c>Task.WhenAll</c>. Credentials are now
/// threaded through as an explicit <see cref="ExchangeCredentials"/> parameter on every call
/// (rather than mutable <c>IExchange.ApiKey</c>/<c>ApiSecret</c> properties), so there is no
/// shared mutable state left to race on. This test still asserts, under real concurrent load
/// with real (not lock-step, not mocked-away) scheduling, that every user's automation cycle only
/// ever passes its own credentials — protecting against ever reintroducing shared mutable
/// credential state that could let one user's calculated orders execute against another user's
/// exchange account.
/// </summary>
[TestClass]
public class AutomationOrchestratorConcurrencyTests
{
  /// <summary>
  /// Records the API key passed to each simulated exchange call, gated behind a real async delay
  /// so that, if credentials were ever shared/mutated across users, every other concurrent
  /// user's write would have ample opportunity to land before this read — turning an
  /// intermittent race into a reliably observable one.
  /// </summary>
  private sealed class CredentialObservationRecorder
  {
    public ConcurrentBag<string> ObservedApiKeys { get; } = [];
  }

  /// <summary>
  /// Minimal <see cref="IExchange"/> fake. Named "BitvavoExchange" so that
  /// <see cref="ExchangeFactory"/>'s type-name-based lookup (which strips the "Exchange" suffix)
  /// resolves it for the hardcoded "Bitvavo" exchange name <see cref="AutomationOrchestrator"/>
  /// currently uses.
  /// </summary>
  private sealed class BitvavoExchange(CredentialObservationRecorder recorder) : IExchange
  {
    public ILogger<IExchange>? Logger => null;
    public string QuoteSymbol => "EUR";
    public decimal MinOrderSizeInQuote => 1;
    public decimal MakerFee => 0;
    public decimal TakerFee => 0;

    public async Task<Result<Balance, ExchangeErrCodeEnum>> GetBalance(ExchangeCredentials credentials)
    {
      // Widen the race window: only record the credentials after yielding, so that if this
      // instance is ever shared, every other concurrently-running user's call gets a chance to
      // land in between.
      await Task.Delay(50);

      recorder.ObservedApiKeys.Add(credentials.ApiKey);

      // Short-circuits AutomationOrchestrator.RunAsync right after credential use via the
      // existing AuthenticationError path, so this test needs no further downstream mocking.
      return Result<Balance, ExchangeErrCodeEnum>.Failure(default, ExchangeErrCodeEnum.AuthenticationError);
    }

    public Task<Result<decimal, ExchangeErrCodeEnum>> TotalDeposited(ExchangeCredentials credentials)
    {
      throw new NotSupportedException();
    }

    public Task<Result<decimal, ExchangeErrCodeEnum>> TotalWithdrawn(ExchangeCredentials credentials)
    {
      throw new NotSupportedException();
    }

    public Task<MarketDataDto?> GetMarket(ExchangeCredentials credentials, MarketReqDto market)
    {
      throw new NotSupportedException();
    }

    public Task<AssetDataDto?> GetAsset(ExchangeCredentials credentials, string baseSymbol)
    {
      throw new NotSupportedException();
    }

    public Task<decimal> GetPrice(ExchangeCredentials credentials, MarketReqDto market)
    {
      throw new NotSupportedException();
    }

    public Task<BestBidAskDto?> GetBestBidAsk(ExchangeCredentials credentials, MarketReqDto market)
    {
      throw new NotSupportedException();
    }

    public Task<Result<OrderDto, ExchangeErrCodeEnum>> NewOrder(ExchangeCredentials credentials, OrderReqDto order, string source = "API")
    {
      throw new NotSupportedException();
    }

    public Task<OrderDto?> GetOrder(ExchangeCredentials credentials, string orderId, MarketReqDto market)
    {
      throw new NotSupportedException();
    }

    public Task<OrderDto?> CancelOrder(ExchangeCredentials credentials, string orderId, MarketReqDto market, string source = "API")
    {
      throw new NotSupportedException();
    }

    public Task<IEnumerable<OrderDto>?> GetOpenOrders(ExchangeCredentials credentials, MarketReqDto? market = null)
    {
      throw new NotSupportedException();
    }

    public Task<IEnumerable<OrderDto>?> CancelAllOpenOrders(ExchangeCredentials credentials, MarketReqDto? market = null, string source = "API")
    {
      throw new NotSupportedException();
    }

    public Task<Result<IEnumerable<OrderDto>?, ExchangeErrCodeEnum>> SellAllPositions(ExchangeCredentials credentials, string? baseSymbol = null, string source = "API")
    {
      throw new NotSupportedException();
    }
  }

  private sealed class FakeApiCredentialsRepository(IReadOnlyDictionary<Guid, string> apiKeysByUser) : IApiCredentialsRepository
  {
    public Task<ApiCredReqDto> GetApiCred(Guid userId, string exchangeName)
    {
      return Task.FromResult(new ApiCredReqDto { ApiKey = apiKeysByUser[userId], ApiSecret = apiKeysByUser[userId] });
    }

    public Task<ApiCredentialStatus?> GetApiCredStatus(Guid userId, string exchangeName)
    {
      return Task.FromResult<ApiCredentialStatus?>(new ApiCredentialStatus(DateTimeOffset.UtcNow));
    }

    public Task SaveApiCred(Guid userId, string exchangeName, ApiCredReqDto apiCred)
    {
      return Task.CompletedTask;
    }
  }

  private sealed class FakeConfigRepository(IReadOnlyDictionary<Guid, ConfigReqDto> configs) : IConfigRepository
  {
    public Task<ConfigReqDto> GetConfig(Guid userId)
    {
      return Task.FromResult(configs[userId]);
    }

    public Task<IEnumerable<KeyValuePair<Guid, ConfigReqDto>>> GetConfigs()
    {
      return Task.FromResult<IEnumerable<KeyValuePair<Guid, ConfigReqDto>>>(configs.ToList());
    }

    public Task<int> SaveConfig(Guid userId, ConfigReqDto configReqDto)
    {
      return Task.FromResult(1);
    }
  }

  [TestMethod]
  public async Task RunAsync_ProcessesManyUsersConcurrently_NeverObservesAnotherUsersApiKey()
  {
    // Arrange
    const int userCount = 20;

    var userIds = Enumerable.Range(1, userCount).Select(_ => Guid.NewGuid()).ToList();

    var apiKeysByUser = userIds.ToDictionary(userId => userId, userId => $"user-{userId}-api-key");

    var configs = userIds.ToDictionary(userId => userId, _ => new ConfigReqDto
    {
      AutomationEnabled = true,
      LastRebalance = null,
    });

    var services = new ServiceCollection();
    services.AddSingleton<CredentialObservationRecorder>();
    services.AddScoped<BitvavoExchange>();
    services.AddScoped(sp => new ExchangeFactory(sp, [typeof(BitvavoExchange)]));
    // Registered Scoped (not passed directly to the orchestrator's constructor) to mirror
    // production DI: AutomationOrchestrator resolves IApiCredentialsRepository/IConfigRepository
    // from the same per-user scope as the exchange, so a fresh instance is created for each
    // concurrently-running user — exactly like EfApiCredentialsRepository/EfConfigRepository do
    // with their shared, non-thread-safe TraderEngineDbContext in production.
    services.AddScoped<IApiCredentialsRepository>(_ => new FakeApiCredentialsRepository(apiKeysByUser));
    services.AddScoped<IConfigRepository>(_ => new FakeConfigRepository(configs));

    var emailNotification = Substitute.For<IEmailNotificationService>();
    emailNotification.SendAutomationApiAuthFailed(Arg.Any<Guid>(), Arg.Any<DateTime>()).Returns(Task.CompletedTask);
    services.AddScoped(_ => emailNotification);

    await using var provider = services.BuildServiceProvider();

    var recorder = provider.GetRequiredService<CredentialObservationRecorder>();

    var environment = Substitute.For<IHostEnvironment>();
    environment.EnvironmentName.Returns(Environments.Production);

    var orchestrator = new AutomationOrchestrator(
      NullLogger<AutomationOrchestrator>.Instance,
      environment,
      provider.GetRequiredService<IServiceScopeFactory>(),
      Substitute.For<IRebalancingService>(),
      Substitute.For<IMarketCapService>(),
      new FakeConfigRepository(configs));

    // Act
    await orchestrator.RunAsync(DateTimeOffset.UtcNow, CancellationToken.None);

    // Assert
    Assert.AreEqual(userCount, recorder.ObservedApiKeys.Count,
      "Every user's automation cycle should have reached the exchange call exactly once.");

    // With correct per-user credential threading, every observed key deterministically equals
    // that call's own user's key, regardless of scheduling — no shared mutable state exists to
    // race on. If credentials were ever held as shared mutable state again, concurrent writes
    // would clobber each other during the delay above, and this would observe duplicated/foreign
    // keys instead of the full distinct set.
    CollectionAssert.AreEquivalent(
      apiKeysByUser.Values.ToList(),
      recorder.ObservedApiKeys.ToList(),
      "An exchange call observed another user's API key — credentials are leaking across concurrent automation cycles, which can execute one user's orders against another user's exchange account.");
  }

  /// <summary>
  /// Safeguards against a local Development environment (e.g. a dev database seeded with real,
  /// migrated production credentials — see TraderEngine.Migration) ever placing a real exchange
  /// order, regardless of how many users have automation enabled.
  /// </summary>
  [TestMethod]
  public async Task RunAsync_InDevelopmentEnvironment_NeverContactsAnyExchange()
  {
    // Arrange
    var userIds = Enumerable.Range(1, 3).Select(_ => Guid.NewGuid()).ToList();

    var apiKeysByUser = userIds.ToDictionary(userId => userId, userId => $"user-{userId}-api-key");

    var configs = userIds.ToDictionary(userId => userId, _ => new ConfigReqDto
    {
      AutomationEnabled = true,
      LastRebalance = null,
    });

    var services = new ServiceCollection();
    services.AddSingleton<CredentialObservationRecorder>();
    services.AddScoped<BitvavoExchange>();
    services.AddScoped(sp => new ExchangeFactory(sp, [typeof(BitvavoExchange)]));
    services.AddScoped<IApiCredentialsRepository>(_ => new FakeApiCredentialsRepository(apiKeysByUser));
    services.AddScoped<IConfigRepository>(_ => new FakeConfigRepository(configs));
    services.AddScoped(_ => Substitute.For<IEmailNotificationService>());

    await using var provider = services.BuildServiceProvider();

    var recorder = provider.GetRequiredService<CredentialObservationRecorder>();

    var environment = Substitute.For<IHostEnvironment>();
    environment.EnvironmentName.Returns(Environments.Development);

    var orchestrator = new AutomationOrchestrator(
      NullLogger<AutomationOrchestrator>.Instance,
      environment,
      provider.GetRequiredService<IServiceScopeFactory>(),
      Substitute.For<IRebalancingService>(),
      Substitute.For<IMarketCapService>(),
      new FakeConfigRepository(configs));

    // Act
    await orchestrator.RunAsync(DateTimeOffset.UtcNow, CancellationToken.None);

    // Assert
    Assert.IsEmpty(recorder.ObservedApiKeys,
      "Automation must not contact any exchange while running in the Development environment, " +
      "regardless of how many users have automation enabled.");
  }
}
