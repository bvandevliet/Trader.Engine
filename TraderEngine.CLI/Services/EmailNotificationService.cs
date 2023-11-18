using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using System.Web;
using TraderEngine.CLI.AppSettings;
using TraderEngine.CLI.Repositories;
using TraderEngine.Common.DTOs.API.Response;

namespace TraderEngine.CLI.Services;

internal class EmailNotificationService : IEmailNotificationService
{
  private readonly EmailSettings _emailSettings;
  private readonly IConfigRepository _configRepo;

  public EmailNotificationService(
    IOptions<EmailSettings> emailOptions,
    IConfigRepository configRepo)
  {
    _emailSettings = emailOptions.Value;
    _configRepo = configRepo;
  }

  public async Task SendAutomationSucceeded(
    int userId, DateTime timestamp, RebalanceDto rebalanceDto)
  {
    var userInfo = await _configRepo.GetUserInfo(userId);

    string htmlString =
      $"<p>Dear {HttpUtility.HtmlEncode("")},</p>" +
      $"<p>An automatic portfolio rebalance was triggered at {timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss}" +
      $" and executed successfully.</p>" +
      $"<p>The below {rebalanceDto.Orders.Length} orders were executed:</p>" +
      $"<pre>{string.Join("</pre><pre>", (object[])rebalanceDto.Orders)}</pre>";

    using var smtpClient = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.SmtpPort);

    smtpClient.Credentials = new NetworkCredential(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword);
    smtpClient.EnableSsl = true;

    using var mailMessage = new MailMessage();

    mailMessage.From = new MailAddress(_emailSettings.FromAddress, "Trader");
    mailMessage.To.Add(userInfo.user_email);
    mailMessage.Subject = "Trader automation succeeded";
    mailMessage.IsBodyHtml = true;
    mailMessage.Body = htmlString;

    await smtpClient.SendMailAsync(mailMessage);
  }

  public async Task SendAutomationFailed(
    int userId, DateTime timestamp, RebalanceDto rebalanceDto)
  {
    var userInfo = await _configRepo.GetUserInfo(userId);

    string htmlString =
      $"<p>Dear {HttpUtility.HtmlEncode("")},</p>" +
      $"<p>An automatic portfolio rebalance was triggered at {timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss}" +
      $" but failed.</p>" +
      $"<p>The below {rebalanceDto.Orders.Length} orders were attempted:</p>" +
      $"<pre>{string.Join("</pre><pre>", (object[])rebalanceDto.Orders)}</pre>";

    using var smtpClient = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.SmtpPort);

    smtpClient.Credentials = new NetworkCredential(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword);
    smtpClient.EnableSsl = true;

    using var mailMessage = new MailMessage();

    mailMessage.From = new MailAddress(_emailSettings.FromAddress, "Trader");
    mailMessage.To.Add(userInfo.user_email);
    mailMessage.Subject = "Trader automation failed";
    mailMessage.IsBodyHtml = true;
    mailMessage.Body = htmlString;

    await smtpClient.SendMailAsync(mailMessage);
  }

  public Task SendAutomationException(
    int userId, DateTime timestamp, Exception exception)
  {
    string htmlString =
      $"<p>Dear {HttpUtility.HtmlEncode("")},</p>" +
      $"<p>An automatic portfolio rebalance was triggered at {timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss}" +
      $" but failed with an exception:</p>" +
      $"<p>{exception.Message}:</p>" +
      $"<pre>{exception.StackTrace}</pre>";

    using var smtpClient = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.SmtpPort);

    smtpClient.Credentials = new NetworkCredential(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword);
    smtpClient.EnableSsl = true;

    using var mailMessage = new MailMessage();

    mailMessage.From = new MailAddress(_emailSettings.FromAddress, "Trader");
    mailMessage.To.Add(_emailSettings.FromAddress);
    mailMessage.Subject = "Trader automation exception";
    mailMessage.IsBodyHtml = true;
    mailMessage.Body = htmlString;

    return smtpClient.SendMailAsync(mailMessage);
  }
}
