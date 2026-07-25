using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TraderEngine.Data.Extensions;

public static class DataProtectionExtensions
{
  /// <summary>
  /// Persists the Data Protection key ring to <paramref name="keyRingPath"/>, shared identically
  /// by TraderEngine.API and TraderEngine.Web so either host can decrypt exchange credentials the
  /// other encrypted. If <c>DataProtection:CertificatePfxBase64</c> is configured, the key ring
  /// itself is encrypted at rest with that certificate — without it, keys are stored as plaintext
  /// XML, which is acceptable for local development but not for a deployment handling real
  /// exchange credentials.
  /// </summary>
  public static IServiceCollection AddSharedDataProtection(this IServiceCollection services, IConfiguration configuration, string keyRingPath)
  {
    var dataProtectionBuilder = services
      .AddDataProtection()
      .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
      .SetApplicationName("TraderEngine");

    var certBase64 = configuration["DataProtection:CertificatePfxBase64"];

    if (!string.IsNullOrWhiteSpace(certBase64))
    {
      var certPassword = configuration["DataProtection:CertificatePassword"] ?? string.Empty;
      var certBytes = Convert.FromBase64String(certBase64);
      var certificate = X509CertificateLoader.LoadPkcs12(certBytes, certPassword, X509KeyStorageFlags.EphemeralKeySet);

      dataProtectionBuilder.ProtectKeysWithCertificate(certificate);
    }

    return services;
  }
}
