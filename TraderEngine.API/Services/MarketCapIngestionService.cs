using System.Threading.Channels;
using TraderEngine.API.Repositories;
using TraderEngine.Common.Repositories;

namespace TraderEngine.API.Services;

/// <summary>
/// Periodically ingests fresh market cap data and, on success, signals
/// <see cref="AutomationRebalancingService"/> to run an automation cycle against it.
/// Replaces the previous cron-triggered <c>-marketcap</c> CLI invocation with an in-process,
/// self-scheduling loop aligned to the top of every hour.
/// </summary>
public sealed class MarketCapIngestionService : BackgroundService
{
  // TODO: Put quote symbol for market cap records in appsettings.
  private readonly string _quoteSymbol = "EUR";

  private readonly IServiceScopeFactory _scopeFactory;
  private readonly ChannelWriter<DateTimeOffset> _channelWriter;
  private readonly ILogger<MarketCapIngestionService> _logger;

  public MarketCapIngestionService(
    IServiceScopeFactory scopeFactory,
    ChannelWriter<DateTimeOffset> channelWriter,
    ILogger<MarketCapIngestionService> logger)
  {
    _scopeFactory = scopeFactory;
    _channelWriter = channelWriter;
    _logger = logger;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      var nextRun = NextHourBoundary(DateTimeOffset.UtcNow);

      var delay = nextRun - DateTimeOffset.UtcNow;

      if (delay > TimeSpan.Zero)
      {
        try
        {
          await Task.Delay(delay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
          break;
        }
      }

      await RunCycleAsync(nextRun);
    }
  }

  private async Task RunCycleAsync(DateTimeOffset timestamp)
  {
    await using var scope = _scopeFactory.CreateAsyncScope();

    var marketCapExtRepo = scope.ServiceProvider.GetRequiredService<IMarketCapExternalRepository>();
    var marketCapIntRepo = scope.ServiceProvider.GetRequiredService<IMarketCapInternalRepository>();

    try
    {
      _ = await marketCapIntRepo.CleanupDatabase();

      _logger.LogInformation("Updating market cap data ..");

      var latest = await marketCapExtRepo.ListLatest(_quoteSymbol);

      _ = await marketCapIntRepo.TryInsertMany(latest);

      if (!_channelWriter.TryWrite(timestamp))
      {
        _logger.LogWarning(
          "Failed to signal automation cycle for {Timestamp} — a signal was already pending.", timestamp);
      }
    }
    catch (Exception ex)
    {
      _logger.LogCritical(ex, "Error while updating market cap data.");

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

  private static DateTimeOffset NextHourBoundary(DateTimeOffset from)
  {
    var thisHour = new DateTimeOffset(from.Year, from.Month, from.Day, from.Hour, 0, 0, from.Offset);

    // Mirrors the previous cron schedule ("2 * * * *" — minute 2 of every hour).
    var thisRun = thisHour.AddMinutes(2);

    return thisRun > from ? thisRun : thisRun.AddHours(1);
  }
}
