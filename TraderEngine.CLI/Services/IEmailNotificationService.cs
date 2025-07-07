using TraderEngine.Common.DTOs.API.Response;

namespace TraderEngine.CLI.Services;

public interface IEmailNotificationService
{
  Task SendAutomationSucceeded(int userId, DateTime timestamp, decimal totalDeposited, decimal totalWithdrawn, SimulationDto simulated, OrderDto[] ordersExecuted);

  Task SendAutomationFailed(int userId, DateTime timestamp, string reason, OrderDto[]? ordersExecuted, object debugData, bool sendAdmin = true);

  Task SendAutomationApiAuthFailed(int userId, DateTime timestamp);

  Task SendAutomationException(int userId, DateTime timestamp, Exception exception);

  Task SendWorkerException(DateTime timestamp, Exception exception);
}