using TraderEngine.Common.DTOs.API.Response;

namespace TraderEngine.CLI.Services;

public interface IEmailNotificationService
{
  public Task SendAutomationSucceeded(int userId, DateTime timestamp, decimal totalDeposited, decimal totalWithdrawn, SimulationDto simulated, OrderDto[] ordersExecuted);

  public Task SendAutomationFailed(int userId, DateTime timestamp, string reason, OrderDto[]? ordersExecuted, object debugData, bool sendAdmin = true);

  public Task SendAutomationApiAuthFailed(int userId, DateTime timestamp);

  public Task SendAutomationException(int userId, DateTime timestamp, Exception exception);

  public Task SendWorkerException(DateTime timestamp, Exception exception);
}