using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;
using System.Text.Json;
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

  private readonly string _cssString =
@"
pre,
code,
kbd,
tt,
var,
.monospace {
  font-family: monospace;
  white-space: pre;
  background-color: unset;
}";

  public async Task SendAutomationSucceeded(
    int userId, DateTime timestamp, decimal totalDeposited, decimal totalWithdrawn, RebalanceDto rebalanceDto)
  {
    var userInfo = await _configRepo.GetUserInfo(userId);

    decimal cumulativeValue = rebalanceDto.NewBalance.AmountQuoteTotal + totalWithdrawn;

    string htmlString =
    $"<style>{_cssString}</style>" +
    $"<p>Hi {HttpUtility.HtmlEncode(userInfo.display_name)},</p>" +
    $"<p>An automatic portfolio rebalance was triggered at {timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss} and executed successfully!</p>" +
    $"<p>Your current balance summary:<br>" +
    $"<table>" +
    $"<tr>" +
    $"<td>Total deposited</td>" +
    $"<td class=\"monospace\" style=\"text-align:right;\">(i)</td>" +
    $"<td class=\"monospace\">:</td>" +
    $"<td class=\"monospace\" style=\"text-align:right;\">{totalDeposited.Round(2)}</td>" +
    $"<td class=\"monospace\">{rebalanceDto.NewBalance.QuoteSymbol}</td>" +
    $"</tr><tr>" +
    $"<td>Total withdrawn</td>" +
    $"<td class=\"monospace\" style=\"text-align:right;\">(o)</td>" +
    $"<td class=\"monospace\">:</td>" +
    $"<td class=\"monospace\" style=\"text-align:right;\">{totalWithdrawn.Round(2)}</td>" +
    $"<td class=\"monospace\">{rebalanceDto.NewBalance.QuoteSymbol}</td>" +
    $"</tr><tr>" +
    $"<td>Current value</td>" +
    $"<td class=\"monospace\" style=\"text-align:right;\">(v)</td>" +
    $"<td class=\"monospace\">:</td>" +
    $"<td class=\"monospace\" style=\"text-align:right;\">{rebalanceDto.NewBalance.AmountQuoteTotal.Floor(2)}</td>" +
    $"<td class=\"monospace\">{rebalanceDto.NewBalance.QuoteSymbol}</td>" +
    $"</tr><tr>" +
    $"<td>Cumulative value</td>" +
    $"<td class=\"monospace\" style=\"text-align:right;\">(V=o+v)</td>" +
    $"<td class=\"monospace\">:</td>" +
    $"<td class=\"monospace\" style=\"text-align:right;\">{cumulativeValue.Floor(2)}</td>" +
    $"<td class=\"monospace\">{rebalanceDto.NewBalance.QuoteSymbol}</td>" +
    $"</tr><tr style=\"border-top-width:1px;\">" +
    $"<td>Total gain</td>" +
    $"<td class=\"monospace\" style=\"text-align:right;\">(V-i)</td>" +
    $"<td class=\"monospace\">:</td>" +
    $"<td class=\"monospace\" style=\"text-align:right;\">{(cumulativeValue - totalDeposited).Floor(2)}</td>" +
    $"<td class=\"monospace\">{rebalanceDto.NewBalance.QuoteSymbol}</td>" +
    $"</tr><tr>" +
    $"<td></td>" +
    $"<td class=\"monospace\" style=\"text-align:right;\">(V/i-1)</td>" +
    $"<td class=\"monospace\">:</td>" +
    $"<td class=\"monospace\" style=\"text-align:right;\">{cumulativeValue.GainPerc(totalDeposited, 2)}</td>" +
    $"<td class=\"monospace\">%</td>" +
    $"</tr>" +
    $"</table></p>" +
    $"<p>The below {rebalanceDto.Orders.Length} orders were executed" +
    $" with a total fee paid of {rebalanceDto.TotalFee.Ceiling(2)} {rebalanceDto.NewBalance.QuoteSymbol}.</p>" +
    $"<table>" +
      string.Concat(rebalanceDto.Orders.Select(order =>
      $"<tr>" +
      $"<td>{(order.Side == OrderSide.Buy ? "Bought" : "Sold")}</td>" +
      $"<td class=\"monospace\" style=\"text-align:right;\">{order.AmountFilled}</td>" +
      $"<td class=\"monospace\">{order.Market.BaseSymbol}</td>" +
      $"<td>for</td>" +
      $"<td class=\"monospace\" style=\"text-align:right;\">{order.AmountQuoteFilled.Round(2)}</td>" +
      $"<td class=\"monospace\">{order.Market.QuoteSymbol}</td>" +
      $"</tr>")) +
    $"</table>" +
    $"<p>This email was automatically generated. Happy trading!</p>";

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
    int userId, DateTime timestamp, RebalanceDto rebalanceDto, object debugData)
  {
    var userInfo = await _configRepo.GetUserInfo(userId);

    string userMsgBody =
    $"<style>{_cssString}</style>" +
    $"<p>Hi {HttpUtility.HtmlEncode(userInfo.display_name)},</p>" +
    $"<p>An automatic portfolio rebalance was triggered at {timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss} but failed!<br>" +
    $"We will try again within an hour.</p>" +
    $"<p>The below {rebalanceDto.Orders.Length} orders were attempted:</p>" +
    $"<pre>{string.Join("</pre><pre>", (object[])rebalanceDto.Orders)}</pre>" +
    $"<p>This email was automatically generated. Happy trading!</p>";

    string adminMsgBody =
    $"<style>{_cssString}</style>" +
    $"<p>Hi Admin,</p>" +
    $"<p>An automatic portfolio rebalance for user {userId} ({userInfo.display_name}) was triggered at {timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss} but failed!</p>" +
    $"<p>Debug data:</p>" +
    $"<pre>{JsonSerializer.Serialize(debugData, debugData.GetType(), new JsonSerializerOptions() { WriteIndented = true })}</pre>" +
    $"<p>This email was automatically generated. Happy trading!</p>";

    using var userMessage = new MimeMessage();

    userMessage.From.Add(new MailboxAddress("Trader Bot", _emailSettings.FromAddress));
    userMessage.To.Add(new MailboxAddress(userInfo.display_name, userInfo.user_email));
    userMessage.Subject = "Trader automation failed";
    userMessage.Body = new TextPart(TextFormat.Html) { Text = userMsgBody };

    using var adminMessage = new MimeMessage();

    adminMessage.From.Add(new MailboxAddress("Trader Bot", _emailSettings.FromAddress));
    adminMessage.To.Add(new MailboxAddress("Trader Admin", _emailSettings.FromAddress));
    adminMessage.Subject = "Trader automation failed";
    adminMessage.Body = new TextPart(TextFormat.Html) { Text = adminMsgBody };

    using var client = new SmtpClient();

    client.Connect(_emailSettings.SmtpServer, _emailSettings.SmtpPort, true);
    client.Authenticate(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword);
    await client.SendAsync(userMessage);
    await client.SendAsync(adminMessage);
    await client.DisconnectAsync(true);
  }

  public async Task SendAutomationException(
    int userId, DateTime timestamp, Exception exception)
  {
    var userInfo = await _configRepo.GetUserInfo(userId);

    string htmlString =
    $"<style>{_cssString}</style>" +
    $"<p>Hi Admin,</p>" +
    $"<p>An automatic portfolio rebalance for user {userId} ({userInfo.display_name}) was triggered at {timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss} but failed with an exception:</p>" +
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
