namespace TraderEngine.Data.Entities;

/// <summary>
/// A user's Data-Protection-encrypted API credentials for a single exchange.
/// <see cref="ProtectedApiKey"/>/<see cref="ProtectedApiSecret"/> are opaque ciphertext produced
/// by an <see cref="Microsoft.AspNetCore.DataProtection.IDataProtector"/> — never plaintext.
/// </summary>
public class ExchangeApiCredential
{
  public Guid Id { get; set; }

  public Guid UserId { get; set; }

  public AppUser User { get; set; } = null!;

  public string ExchangeName { get; set; } = string.Empty;

  public string ProtectedApiKey { get; set; } = string.Empty;

  public string ProtectedApiSecret { get; set; } = string.Empty;

  public DateTimeOffset UpdatedAt { get; set; }
}
