using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;
using System.Web;
using TraderEngine.CLI.AppSettings;
using TraderEngine.CLI.Repositories;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Extensions;

namespace TraderEngine.CLI.Services;

public class EmailNotificationService : IEmailNotificationService
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

    var orderData = rebalanceDto.Orders.Select(order =>
    $"{(order.Side == OrderSide.Buy ? "Bought" : "Sold")}\n" +
    $"{order.AmountFilled} {order.Market.BaseSymbol}\n" +
    $"for {order.AmountQuoteFilled.Floor(2)} {order.Market.QuoteSymbol}");

    string htmlString =
      $"<p>Hi {HttpUtility.HtmlEncode(userInfo.display_name)},</p>" +
      $"<p>An automatic portfolio rebalance was triggered at {timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss} and executed successfully!</p>" +
      $"<p>A total fee of {rebalanceDto.Orders.Sum(order => order.FeePaid).Ceiling(2)} {rebalanceDto.NewBalance.QuoteSymbol} was paid.</p>" +
      $"<p>The below {rebalanceDto.Orders.Length} orders were executed:</p>" +
      $"<pre>{string.Join("</pre><pre>", orderData)}</pre>";

    using var message = new MimeMessage();

    message.From.Add(new MailboxAddress("Trader Bot", _emailSettings.FromAddress));
    message.To.Add(new MailboxAddress(userInfo.display_name, userInfo.user_email));
    message.Subject = "Trader automation succeeded";
    message.Body = new TextPart(TextFormat.Html) { Text = htmlString };

    using var client = new SmtpClient();

    client.Connect(_emailSettings.SmtpServer, _emailSettings.SmtpPort, true);
    client.Authenticate(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword);
    await client.SendAsync(message);
    await client.DisconnectAsync(true);
  }

  public async Task SendAutomationFailed(
    int userId, DateTime timestamp, RebalanceDto rebalanceDto)
  {
    var userInfo = await _configRepo.GetUserInfo(userId);

    string htmlString =
      $"<p>Hi {HttpUtility.HtmlEncode(userInfo.display_name)},</p>" +
      $"<p>An automatic portfolio rebalance was triggered at {timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss} but failed!</p>" +
      $"<p>The below {rebalanceDto.Orders.Length} orders were attempted:</p>" +
      $"<pre>{string.Join("</pre><pre>", (object[])rebalanceDto.Orders)}</pre>";

    using var message = new MimeMessage();

    message.From.Add(new MailboxAddress("Trader Bot", _emailSettings.FromAddress));
    message.To.Add(new MailboxAddress(userInfo.display_name, userInfo.user_email));
    message.Subject = "Trader automation failed";
    message.Body = new TextPart(TextFormat.Html) { Text = htmlString };

    using var client = new SmtpClient();

    client.Connect(_emailSettings.SmtpServer, _emailSettings.SmtpPort, true);
    client.Authenticate(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword);
    await client.SendAsync(message);
    await client.DisconnectAsync(true);
  }

  public async Task SendAutomationException(
    int userId, DateTime timestamp, Exception exception)
  {
    string htmlString =
      $"<p>Hi {HttpUtility.HtmlEncode("")},</p>" +
      $"<p>An automatic portfolio rebalance was triggered at {timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss} but failed with an exception:</p>" +
      $"<p>{exception.Message}:</p>" +
      $"<pre>{exception.StackTrace}</pre>";

    using var message = new MimeMessage();

    message.From.Add(new MailboxAddress("Trader Bot", _emailSettings.FromAddress));
    message.To.Add(new MailboxAddress("Trader Admin", _emailSettings.FromAddress));
    message.Subject = "Trader automation exception";
    message.Body = new TextPart(TextFormat.Html) { Text = htmlString };

    using var client = new SmtpClient();

    client.Connect(_emailSettings.SmtpServer, _emailSettings.SmtpPort, true);
    client.Authenticate(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword);
    await client.SendAsync(message);
    await client.DisconnectAsync(true);
  }
}
