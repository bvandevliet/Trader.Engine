using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TraderEngine.API.DTOs.WordPress;
using TraderEngine.API.Factories;
using TraderEngine.API.Repositories;
using TraderEngine.API.Services;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Exchanges;
using TraderEngine.Common.Models;
using TraderEngine.Common.Results;
using TraderEngine.Common.Services;

namespace TraderEngine.API.Tests.Services;

/// <summary>
/// Guards against a critical class of bug: <see cref="AutomationOrchestrator.RunAsync"/>
/// processes every user's automation concurrently via <c>Task.WhenAll</c>. If it were to ever
/// resolve <see cref="IExchange"/> from a single shared DI scope again (as it did before the
/// per-user-scope fix), concurrent users could share one mutable exchange instance and race on
/// its <see cref="IExchange.ApiKey"/>/<see cref="IExchange.ApiSecret"/> — meaning one user's
/// calculated orders could execute against another user's exchange account. This must never
/// happen, so this test asserts, under real concurrent load with real (not lock-step, not
/// mocked-away) scheduling, that every user's automation cycle only ever observes its own
/// credentials.
/// </summary>
[TestClass]
public class AutomationOrchestratorConcurrencyTests
{
  /// <summary>
  /// Records the API key an <see cref="IExchange"/> instance actually had set on it at the
  /// moment each simulated exchange call was served, gated behind a real async delay so that,
  /// if the exchange instance were shared across users, every other concurrent user's credential
  /// write has ample opportunity to land before this read — turning an intermittent race into a
  /// reliably observable one.
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
    public string ApiKey { get; set; } = "";
    public string ApiSecret { get; set; } = "";

    public async Task<Result<Balance, ExchangeErrCodeEnum>> GetBalance()
    {
      // Widen the race window: only read back ApiKey after yielding, so that if this instance
      // is shared, every other concurrently-running user's credential write gets a chance to
      // land in between their own write and this read.
      await Task.Delay(50);

      recorder.ObservedApiKeys.Add(ApiKey);

      // Short-circuits AutomationOrchestrator.RunAsync right after credential use via the
      // existing AuthenticationError path, so this test needs no further downstream mocking.
      return Result<Balance, ExchangeErrCodeEnum>.Failure(default, ExchangeErrCodeEnum.AuthenticationError);
    }

    public Task<Result<decimal, ExchangeErrCodeEnum>> TotalDeposited() => throw new NotSupportedException();
    public Task<Result<decimal, ExchangeErrCodeEnum>> TotalWithdrawn() => throw new NotSupportedException();
    public Task<MarketDataDto?> GetMarket(MarketReqDto market) => throw new NotSupportedException();
    public Task<AssetDataDto?> GetAsset(string baseSymbol) => throw new NotSupportedException();
    public Task<decimal> GetPrice(MarketReqDto market) => throw new NotSupportedException();
    public Task<Result<OrderDto, ExchangeErrCodeEnum>> NewOrder(OrderReqDto order, string source = "API") => throw new NotSupportedException();
    public Task<OrderDto?> GetOrder(string orderId, MarketReqDto market) => throw new NotSupportedException();
    public Task<OrderDto?> CancelOrder(string orderId, MarketReqDto market, string source = "API") => throw new NotSupportedException();
    public Task<IEnumerable<OrderDto>?> GetOpenOrders(MarketReqDto? market = null) => throw new NotSupportedException();
    public Task<IEnumerable<OrderDto>?> CancelAllOpenOrders(MarketReqDto? market = null, string source = "API") => throw new NotSupportedException();
    public Task<Result<IEnumerable<OrderDto>?, ExchangeErrCodeEnum>> SellAllPositions(string? baseSymbol = null, string source = "API") => throw new NotSupportedException();
  }

  private sealed class FakeApiCredentialsRepository(IReadOnlyDictionary<int, string> apiKeysByUser) : IApiCredentialsRepository
  {
    public Task<ApiCredReqDto> GetApiCred(int userId, string exchangeName) =>
      Task.FromResult(new ApiCredReqDto { ApiKey = apiKeysByUser[userId], ApiSecret = apiKeysByUser[userId] });
  }

  private sealed class FakeConfigRepository(IReadOnlyDictionary<int, ConfigReqDto> configs) : IConfigRepository
  {
    public Task<WordPressUserDto> GetUserInfo(int userId) =>
      Task.FromResult(new WordPressUserDto { user_login = $"user{userId}", display_name = $"User {userId}", user_email = $"user{userId}@test.local" });

    public Task<ConfigReqDto> GetConfig(int userId) => Task.FromResult(configs[userId]);

    public Task<IEnumerable<KeyValuePair<int, ConfigReqDto>>> GetConfigs() =>
      Task.FromResult<IEnumerable<KeyValuePair<int, ConfigReqDto>>>(configs.ToList());

    public Task<int> SaveConfig(int userId, ConfigReqDto configReqDto) => Task.FromResult(1);
  }

  [TestMethod]
  public async Task RunAsync_ProcessesManyUsersConcurrently_NeverObservesAnotherUsersApiKey()
  {
    // Arrange
    const int userCount = 20;

    var apiKeysByUser = Enumerable.Range(1, userCount)
      .ToDictionary(userId => userId, userId => $"user-{userId}-api-key");

    var configs = apiKeysByUser.Keys.ToDictionary(userId => userId, _ => new ConfigReqDto
    {
      AutomationEnabled = true,
      LastRebalance = null,
    });

    var services = new ServiceCollection();
    services.AddSingleton<CredentialObservationRecorder>();
    services.AddScoped<BitvavoExchange>();
    services.AddScoped(sp => new ExchangeFactory(sp, [typeof(BitvavoExchange)]));

    await using var provider = services.BuildServiceProvider();

    var recorder = provider.GetRequiredService<CredentialObservationRecorder>();

    var emailNotification = Substitute.For<IEmailNotificationService>();
    emailNotification.SendAutomationApiAuthFailed(Arg.Any<int>(), Arg.Any<DateTime>()).Returns(Task.CompletedTask);

    var orchestrator = new AutomationOrchestrator(
      NullLogger<AutomationOrchestrator>.Instance,
      provider.GetRequiredService<IServiceScopeFactory>(),
      Substitute.For<IRebalancingService>(),
      Substitute.For<IMarketCapService>(),
      new FakeApiCredentialsRepository(apiKeysByUser),
      new FakeConfigRepository(configs),
      emailNotification);

    // Act
    await orchestrator.RunAsync(DateTimeOffset.UtcNow, CancellationToken.None);

    // Assert
    Assert.AreEqual(userCount, recorder.ObservedApiKeys.Count,
      "Every user's automation cycle should have reached the exchange call exactly once.");

    // With correct per-user DI scoping, every observed key deterministically equals that call's
    // own user's key, regardless of scheduling — no shared mutable state exists to race on. If
    // exchange instances were ever shared again, concurrent writes would clobber each other
    // during the delay above, and this would observe duplicated/foreign keys instead of the
    // full distinct set.
    CollectionAssert.AreEquivalent(
      apiKeysByUser.Values.ToList(),
      recorder.ObservedApiKeys.ToList(),
      "An exchange instance's ApiKey was observed by more than one user — exchange instances are being shared across concurrent automation cycles, which can execute one user's orders against another user's exchange account.");
  }
}
