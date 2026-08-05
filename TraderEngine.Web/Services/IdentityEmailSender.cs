using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MimeKit;
using TraderEngine.Data.Entities;
using TraderEngine.Web.AppSettings;

namespace TraderEngine.Web.Services;

public class IdentityEmailSender : IEmailSender<AppUser>
{
  private readonly EmailSettings _emailSettings;

  public IdentityEmailSender(IOptions<EmailSettings> emailOptions)
  {
    _emailSettings = emailOptions.Value;
  }

  public Task SendConfirmationLinkAsync(AppUser user, string email, string confirmationLink)
  {
    return SendEmail(email, "Confirm your email", $"Confirm your account by <a href='{confirmationLink}'>clicking here</a>.");
  }

  public Task SendPasswordResetLinkAsync(AppUser user, string email, string resetLink)
  {
    return SendEmail(email, "Reset your password", $"Reset your password by <a href='{resetLink}'>clicking here</a>.");
  }

  public Task SendPasswordResetCodeAsync(AppUser user, string email, string resetCode)
  {
    return SendEmail(email, "Reset your password", $"Your password reset code is: {resetCode}");
  }

  private async Task SendEmail(string toAddress, string subject, string htmlBody)
  {
    var message = new MimeMessage();
    message.From.Add(MailboxAddress.Parse(_emailSettings.FromAddress));
    message.To.Add(MailboxAddress.Parse(toAddress));
    message.Subject = subject;
    message.Body = new TextPart(MimeKit.Text.TextFormat.Html) { Text = htmlBody };

    using var client = new SmtpClient();
    await client.ConnectAsync(_emailSettings.SmtpServer, _emailSettings.SmtpPort);
    await client.AuthenticateAsync(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword);
    await client.SendAsync(message);
    await client.DisconnectAsync(true);
  }
}
