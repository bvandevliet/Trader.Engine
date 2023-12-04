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

  private readonly string _cssString =
@"
pre,
code,
kbd,
tt,
var,
.monospace,
.trader-number {
  font-family: monospace;
  white-space: pre;
  background-color: unset;
}
.trader-number {
  text-align: right;
}
table tr.trader-number th,
table tr.trader-number td,
table th.trader-number,
table td.trader-number {
  width: .1ch;
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
    $"<td class=\"trader-number\">(i)</td>" +
    $"<td class=\"trader-number\">:</td>" +
    $"<td class=\"trader-number\">{totalDeposited.Round(2)}</td>" +
    $"<td class=\"trader-number\">{rebalanceDto.NewBalance.QuoteSymbol}</td>" +
    $"</tr><tr>" +
    $"<td>Total withdrawn</td>" +
    $"<td class=\"trader-number\">(o)</td>" +
    $"<td class=\"trader-number\">:</td>" +
    $"<td class=\"trader-number\">{totalWithdrawn.Round(2)}</td>" +
    $"<td class=\"trader-number\">{rebalanceDto.NewBalance.QuoteSymbol}</td>" +
    $"</tr><tr>" +
    $"<td>Current value</td>" +
    $"<td class=\"trader-number\">(v)</td>" +
    $"<td class=\"trader-number\">:</td>" +
    $"<td class=\"trader-number\">{rebalanceDto.NewBalance.AmountQuoteTotal.Floor(2)}</td>" +
    $"<td class=\"trader-number\">{rebalanceDto.NewBalance.QuoteSymbol}</td>" +
    $"</tr><tr>" +
    $"<td>Cumulative value</td>" +
    $"<td class=\"trader-number\">(V=o+v)</td>" +
    $"<td class=\"trader-number\">:</td>" +
    $"<td class=\"trader-number\">{cumulativeValue.Floor(2)}</td>" +
    $"<td class=\"trader-number\">{rebalanceDto.NewBalance.QuoteSymbol}</td>" +
    $"</tr><tr style=\"border-top-width:1px;\">" +
    $"<td>Total gain</td>" +
    $"<td class=\"trader-number\">(V-i)</td>" +
    $"<td class=\"trader-number\">:</td>" +
    $"<td class=\"trader-number\">{(cumulativeValue - totalDeposited).Round(2)}</td>" +
    $"<td class=\"trader-number\">{rebalanceDto.NewBalance.QuoteSymbol}</td>" +
    $"</tr><tr>" +
    $"<td></td>" +
    $"<td class=\"trader-number\">(V/i-1)</td>" +
    $"<td class=\"trader-number\">:</td>" +
    $"<td class=\"trader-number\">{cumulativeValue.GainPerc(totalDeposited, 2)}</td>" +
    $"<td class=\"trader-number\">%</td>" +
    $"</tr>" +
    $"</table></p>" +
    $"<p>The below {rebalanceDto.Orders.Length} orders were executed" +
    $" with a total fee paid of {rebalanceDto.TotalFee.Ceiling(2)} {rebalanceDto.NewBalance.QuoteSymbol}.</p>" +
    $"<table>" +
      string.Concat(rebalanceDto.Orders.Select(order =>
      $"<tr>" +
      $"<td>{(order.Side == OrderSide.Buy ? "Bought" : "Sold")}</td>" +
      $"<td class=\"trader-number\">{order.AmountFilled} {order.Market.BaseSymbol}</td>" +
      $"<td class=\"trader-number\">for {order.AmountQuoteFilled.Round(2)} {order.Market.QuoteSymbol}</td>" +
      $"</tr>")) +
    "</table>";

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
    $"<style>{_cssString}</style>" +
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
    $"<style>{_cssString}</style>" +
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
