namespace TraderEngine.API.Services;

public interface IAutomationOrchestrator
{
  /// <summary>
  /// Runs one automation cycle for all users, triggered by a fresh market cap data ingestion.
  /// </summary>
  /// <param name="dataTimestamp">Timestamp of the market cap data this cycle should act on.</param>
  /// <param name="ct"></param>
  Task RunAsync(DateTimeOffset dataTimestamp, CancellationToken ct);
}
