using TraderEngine.Common.DTOs.API.Response;

namespace TraderEngine.CLI.Services;

public interface IEmailNotificationService
{
  Task SendAutomationSucceeded(int userId, DateTime timestamp, RebalanceDto rebalanceDto);

  Task SendAutomationFailed(int userId, DateTime timestamp, RebalanceDto rebalanceDto);

  Task SendAutomationException(int userId, DateTime timestamp, Exception exception);
}