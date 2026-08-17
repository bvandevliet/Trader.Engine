using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TraderEngine.Data.Extensions;

public static class DataProtectionExtensions
{
  /// <summary>
  /// Resolves <c>DataProtection:KeyRingPath</c> the same way for every host that shares the key
  /// ring (TraderEngine.API, TraderEngine.Web) — defaults to a "secrets"
  /// folder next to the executable when unset, passes an absolute path through unchanged, or
  /// resolves a relative path against <paramref name="contentRootPath"/>. Keeping this in one
  /// place prevents the three hosts from silently drifting apart on how the path is derived,
  /// which would otherwise be a hard-to-diagnose failure the first time one host can't decrypt
  /// ciphertext another host produced.
  /// </summary>
  public static string ResolveDataProtectionKeyRingPath(this IConfiguration configuration, string contentRootPath)
  {
    var configuredKeyRingPath = configuration["DataProtection:KeyRingPath"];

    return string.IsNullOrWhiteSpace(configuredKeyRingPath)
      ? Path.Combine(AppContext.BaseDirectory, "secrets")
      : Path.IsPathRooted(configuredKeyRingPath)
        ? configuredKeyRingPath
        : Path.GetFullPath(Path.Combine(contentRootPath, "..", configuredKeyRingPath));
  }

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
