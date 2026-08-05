using TraderEngine.Common.DTOs.API.Response;

namespace TraderEngine.API.Services;

public interface IEmailNotificationService
{
  public Task SendAutomationSucceeded(Guid userId, DateTime timestamp, decimal totalDeposited, decimal totalWithdrawn, SimulationDto simulated, OrderDto[] ordersExecuted);

  public Task SendAutomationFailed(Guid userId, DateTime timestamp, string reason, OrderDto[]? ordersExecuted, object debugData, bool sendAdmin = true);

  public Task SendAutomationApiAuthFailed(Guid userId, DateTime timestamp);

  public Task SendAutomationException(Guid userId, DateTime timestamp, Exception exception);

  public Task SendWorkerException(DateTime timestamp, Exception exception);
}
