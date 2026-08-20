using System.Text.Json;
using System.Web;
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;
using TraderEngine.API.AppSettings;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Enums;
using TraderEngine.Common.Extensions;
using TraderEngine.Data.Entities;

namespace TraderEngine.API.Services;

public class EmailNotificationService : IEmailNotificationService
{
  private readonly EmailSettings _emailSettings;
  private readonly UserManager<AppUser> _userManager;

  // Copy-construct — WriteIndented differs here
  // (pretty-printed for human readability)
  private static readonly JsonSerializerOptions _jsonOptions = new(AppJsonSerializer.Options) { WriteIndented = true };

  public EmailNotificationService(
    IOptions<EmailSettings> emailOptions,
    UserManager<AppUser> userManager)
  {
    _emailSettings = emailOptions.Value;
    _userManager = userManager;
  }

  private async Task<AppUser> GetUserOrThrow(Guid userId)
  {
    return await _userManager.FindByIdAsync(userId.ToString())
      ?? throw new InvalidOperationException($"User '{userId}' not found.");
  }

  // Emails have no client-side JS to localize timestamps with (see localizeTimestamps in the web
  // app's format.ts), so this is the one place that still converts server-side — using the
  // recipient's own stored TimeZoneId rather than the server's, which is what ToLocalTime() would
  // have used. Falls back to labeled UTC if the stored id isn't recognized on this host (e.g. a
  // Windows zone id from a dev machine landing on a Linux production server).
  private static string FormatForUser(DateTime utcTimestamp, string timeZoneId)
  {
    try
    {
      var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
      var local = TimeZoneInfo.ConvertTimeFromUtc(utcTimestamp, timeZone);

      return $"{local:yyyy-MM-dd HH:mm:ss} ({timeZone.Id})";
    }
    catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
    {
      return $"{utcTimestamp:yyyy-MM-dd HH:mm:ss} UTC";
    }
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
}
th,
td {
  padding: 0;
}
th+th,
td+td {
  padding-left: 1ch;
}";

  public async Task SendAutomationSucceeded(
    Guid userId, DateTime timestamp, decimal totalDeposited, decimal totalWithdrawn, SimulationDto simulated, OrderDto[] ordersExecuted)
  {
    var userInfo = await GetUserOrThrow(userId);

    var newAmountQuoteTotal = simulated.NewBalance.AmountQuoteTotal;
    var cumulativeValue = newAmountQuoteTotal + totalWithdrawn;

    var htmlString =
    $"<meta name=\"format-detection\" content=\"telephone=no\">" +
    $"<style>{_cssString}</style>" +
    $"<p>Hi {HttpUtility.HtmlEncode(userInfo.DisplayName)},</p>" +
    $"<p>An automatic portfolio rebalance was triggered at {FormatForUser(timestamp, userInfo.TimeZoneId)} and executed successfully!</p>" +
    $"<p>Your current balance summary:<br>" +
    $"<table class=\"monospace\">" +
    $"<tr>" +
    $"<td>Deposited</td>" +
    $"<td style=\"text-align:right;\">(i)</td>" +
    $"<td>:</td>" +
    $"<td style=\"text-align:right;\">{totalDeposited.Round(2)}</td>" +
    $"<td>{simulated.NewBalance.QuoteSymbol}</td>" +
    $"</tr><tr>" +
    $"<td>Withdrawn</td>" +
    $"<td style=\"text-align:right;\">(o)</td>" +
    $"<td>:</td>" +
    $"<td style=\"text-align:right;\">{totalWithdrawn.Round(2)}</td>" +
    $"<td>{simulated.NewBalance.QuoteSymbol}</td>" +
    $"</tr><tr>" +
    $"<td>Balance</td>" +
    $"<td style=\"text-align:right;\">(v)</td>" +
    $"<td>:</td>" +
    $"<td style=\"text-align:right;\">{simulated.NewBalance.AmountQuoteTotal.Floor(2)}</td>" +
    $"<td>{simulated.NewBalance.QuoteSymbol}</td>" +
    $"</tr><tr>" +
    $"<td>Cumulative</td>" +
    $"<td style=\"text-align:right;\">(V=o+v)</td>" +
    $"<td>:</td>" +
    $"<td style=\"text-align:right;\">{cumulativeValue.Floor(2)}</td>" +
    $"<td>{simulated.NewBalance.QuoteSymbol}</td>" +
    $"</tr><tr style=\"border-top-width:1px;\">" +
    $"<td>Total gain</td>" +
    $"<td style=\"text-align:right;\">(V-i)</td>" +
    $"<td>:</td>" +
    $"<td style=\"text-align:right;\">{(cumulativeValue - totalDeposited).Floor(2)}</td>" +
    $"<td>{simulated.NewBalance.QuoteSymbol}</td>" +
    $"</tr><tr>" +
    $"<td></td>" +
    $"<td style=\"text-align:right;\">(V/i-1)</td>" +
    $"<td>:</td>" +
    $"<td style=\"text-align:right;\">{cumulativeValue.GainPerc(totalDeposited, 2)}</td>" +
    $"<td>%</td>" +
    $"</tr>" +
    $"</table></p>" +
    $"<p>The below {ordersExecuted.Length} orders were executed" +
    $" with a total fee paid of {simulated.TotalFee.Ceiling(2)} {simulated.NewBalance.QuoteSymbol}.</p>" +
    $"<table class=\"monospace\">" +
    string.Concat(ordersExecuted.Where(order => order.Side == OrderSide.Sell).OrderByDescending(order => order.AmountQuoteFilled).Select(order =>
      $"<tr>" +
      $"<td>Sold</td>" +
      $"<td style=\"text-align:right;\">{order.AmountQuoteFilled.Round(2)} {order.Market.QuoteSymbol}</td>" +
      $"<td>of {order.Market.BaseSymbol}</td>" +
      $"</tr>")) +
    string.Concat(ordersExecuted.Where(order => order.Side == OrderSide.Buy).OrderByDescending(order => order.AmountQuoteFilled).Select(order =>
      $"<tr>" +
      $"<td>Bought</td>" +
      $"<td style=\"text-align:right;\">{order.AmountQuoteFilled.Round(2)} {order.Market.QuoteSymbol}</td>" +
      $"<td>of {order.Market.BaseSymbol}</td>" +
      $"</tr>")) +
    $"</table>" +
    $"<p>Below is your new portfolio balance overview.</p>" +
    $"<table class=\"monospace\">" +
    string.Concat(simulated.NewBalance.Allocations.Select(alloc =>
      $"<tr>" +
      $"<td>{alloc.Market.BaseSymbol}</td>" +
      $"<td style=\"text-align:right;\">{alloc.AmountQuote.Round(2)} {alloc.Market.QuoteSymbol}</td>" +
      $"<td style=\"text-align:right;\">{(newAmountQuoteTotal == 0 ? 0 : alloc.AmountQuote / newAmountQuoteTotal * 100).Round(2)} %</td>" +
      $"</tr>")) +
    $"</table>" +
    $"<p>This email was automatically generated. Happy trading!<br>" +
    $"Visit Trader at <a href=\"{_emailSettings.WebsiteUrl}\">{_emailSettings.WebsiteUrl}</a></p>";

    using var message = new MimeMessage();

    message.From.Add(new MailboxAddress("Trader Bot", _emailSettings.FromAddress));
    message.To.Add(new MailboxAddress(userInfo.DisplayName, userInfo.Email!));
    message.Subject = "Trader automation succeeded";
    message.Body = new TextPart(TextFormat.Html) { Text = htmlString };

    using var client = new SmtpClient();

    client.Connect(_emailSettings.SmtpServer, _emailSettings.SmtpPort, true);
    client.Authenticate(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword);
    _ = await client.SendAsync(message);
    await client.DisconnectAsync(true);
  }

  public async Task SendAutomationFailed(
    Guid userId, DateTime timestamp, string reason, OrderDto[]? ordersAttempted, object debugData, bool sendAdmin = true)
  {
    var userInfo = await GetUserOrThrow(userId);

    var userMsgBody =
    $"<meta name=\"format-detection\" content=\"telephone=no\">" +
    $"<style>{_cssString}</style>" +
    $"<p>Hi {HttpUtility.HtmlEncode(userInfo.DisplayName)},</p>" +
    $"<p>An automatic portfolio rebalance was triggered at {FormatForUser(timestamp, userInfo.TimeZoneId)} but failed!<br>" +
    $"We will try again within an hour.</p>" +
    $"<p>Reason: {HttpUtility.HtmlEncode(reason)}</p>" +
    $"<p>The below {ordersAttempted?.Length ?? 0} orders were attempted:</p>" +
    $"<pre>{string.Join("</pre><pre>", (ordersAttempted ?? []).Select(order => HttpUtility.HtmlEncode(order.ToString())))}</pre>" +
    $"<p>This email was automatically generated. Happy trading!" +
    $"Visit Trader at <a href=\"{_emailSettings.WebsiteUrl}\">{_emailSettings.WebsiteUrl}</a></p>";

    var adminMsgBody =
    $"<meta name=\"format-detection\" content=\"telephone=no\">" +
    $"<style>{_cssString}</style>" +
    $"<p>Hi Admin,</p>" +
    $"<p>An automatic portfolio rebalance for user {userId} ({userInfo.DisplayName}) was triggered at {timestamp:yyyy-MM-dd HH:mm:ss} UTC but failed!</p>" +
    $"<p>Reason: {HttpUtility.HtmlEncode(reason)}</p>" +
    $"<p>Debug data:</p>" +
    $"<pre>{JsonSerializer.Serialize(debugData, debugData.GetType(), _jsonOptions)}</pre>" +
    $"<p>This email was automatically generated. Happy trading!<br>" +
    $"Visit Trader at <a href=\"{_emailSettings.WebsiteUrl}\">{_emailSettings.WebsiteUrl}</a></p>";

    using var userMessage = new MimeMessage();

    userMessage.From.Add(new MailboxAddress("Trader Bot", _emailSettings.FromAddress));
    userMessage.To.Add(new MailboxAddress(userInfo.DisplayName, userInfo.Email!));
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
    _ = await client.SendAsync(userMessage);
    if (sendAdmin)
      _ = await client.SendAsync(adminMessage);
    await client.DisconnectAsync(true);
  }

  public async Task SendAutomationApiAuthFailed(
    Guid userId, DateTime timestamp)
  {
    var userInfo = await GetUserOrThrow(userId);

    var userMsgBody =
    $"<meta name=\"format-detection\" content=\"telephone=no\">" +
    $"<style>{_cssString}</style>" +
    $"<p>Hi {HttpUtility.HtmlEncode(userInfo.DisplayName)},</p>" +
    $"<p>An automatic portfolio rebalance was triggered at {FormatForUser(timestamp, userInfo.TimeZoneId)} " +
    $"but failed because exchange API authentication failed!</p>" +
    $"<p>Please update your exchange API key or disable automation.<br>" +
    $"Visit Trader at <a href=\"{_emailSettings.WebsiteUrl}\">{_emailSettings.WebsiteUrl}</a></p>";

    using var userMessage = new MimeMessage();

    userMessage.From.Add(new MailboxAddress("Trader Bot", _emailSettings.FromAddress));
    userMessage.To.Add(new MailboxAddress(userInfo.DisplayName, userInfo.Email!));
    userMessage.Subject = "Trader automation failed";
    userMessage.Body = new TextPart(TextFormat.Html) { Text = userMsgBody };

    using var client = new SmtpClient();

    client.Connect(_emailSettings.SmtpServer, _emailSettings.SmtpPort, true);
    client.Authenticate(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword);
    _ = await client.SendAsync(userMessage);
    await client.DisconnectAsync(true);
  }

  public async Task SendAutomationException(
    Guid userId, DateTime timestamp, Exception exception)
  {
    var userInfo = await GetUserOrThrow(userId);

    var htmlString =
    $"<meta name=\"format-detection\" content=\"telephone=no\">" +
    $"<style>{_cssString}</style>" +
    $"<p>Hi Admin,</p>" +
    $"<p>An automatic portfolio rebalance for user {userId} ({userInfo.DisplayName}) was triggered at {timestamp:yyyy-MM-dd HH:mm:ss} UTC but failed with an exception:</p>" +
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
    _ = await client.SendAsync(message);
    await client.DisconnectAsync(true);
  }

  public async Task SendWorkerException(
    DateTime timestamp, Exception exception)
  {
    var htmlString =
    $"<meta name=\"format-detection\" content=\"telephone=no\">" +
    $"<style>{_cssString}</style>" +
    $"<p>Hi Admin,</p>" +
    $"<p>A Worker exception has occurred at {timestamp:yyyy-MM-dd HH:mm:ss} UTC:</p>" +
    $"<p>{exception.Message}:</p>" +
    $"<pre>{exception.StackTrace}</pre>";

    using var message = new MimeMessage();

    message.From.Add(new MailboxAddress("Trader Bot", _emailSettings.FromAddress));
    message.To.Add(new MailboxAddress("Trader Admin", _emailSettings.FromAddress));
    message.Subject = "Trader Worker exception";
    message.Body = new TextPart(TextFormat.Html) { Text = htmlString };

    using var client = new SmtpClient();

    client.Connect(_emailSettings.SmtpServer, _emailSettings.SmtpPort, true);
    client.Authenticate(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword);
    _ = await client.SendAsync(message);
    await client.DisconnectAsync(true);
  }
}
