using System.Threading.Channels;

namespace TraderEngine.API.Services;

/// <summary>
/// Bridges <see cref="MarketCapIngestionService"/>'s ingestion signal to <see cref="IAutomationOrchestrator"/>.
/// Purely reactive — no internal timer; runs once per successful ingestion signal. Contains no
/// business logic of its own, only DI scope handling and top-level failure containment so one
/// bad cycle can never take down the host.
/// </summary>
public sealed class AutomationRebalancingService : BackgroundService
{
  private readonly ChannelReader<DateTimeOffset> _channelReader;
  private readonly IServiceScopeFactory _scopeFactory;
  private readonly ILogger<AutomationRebalancingService> _logger;

  public AutomationRebalancingService(
    ChannelReader<DateTimeOffset> channelReader,
    IServiceScopeFactory scopeFactory,
    ILogger<AutomationRebalancingService> logger)
  {
    _channelReader = channelReader;
    _scopeFactory = scopeFactory;
    _logger = logger;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    await foreach (var timestamp in _channelReader.ReadAllAsync(stoppingToken))
    {
      await using var scope = _scopeFactory.CreateAsyncScope();

      try
      {
        var orchestrator = scope.ServiceProvider.GetRequiredService<IAutomationOrchestrator>();

        await orchestrator.RunAsync(timestamp, stoppingToken);
      }
      catch (Exception ex) when (ex is not OperationCanceledException)
      {
        _logger.LogCritical(ex, "Error while running automation cycle.");

        try
        {
          var emailNotification = scope.ServiceProvider.GetRequiredService<IEmailNotificationService>();

          await emailNotification.SendWorkerException(DateTime.UtcNow, ex);
        }
        catch (Exception emailEx)
        {
          _logger.LogCritical(emailEx, "Error while sending worker exception notification.");
        }
      }
    }
  }
}
