namespace TraderEngine.CLI.AppSettings;

internal class EmailSettings
{
  public string SmtpServer { get; set; } = "your-smtp-server.com";

  public int SmtpPort { get; set; } = 587;

  public string SmtpUsername { get; set; } = "your-smtp-username";

  public string SmtpPassword { get; set; } = "your-smtp-password";

  public string FromAddress { get; set; } = "your-from-address@email.com";
}