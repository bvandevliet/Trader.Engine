namespace TraderEngine.Migration.AppSettings;

public class MigrationSettings
{
  /// <summary>
  /// Base URL of the legacy <c>traderengine.cryptography</c> microservice, still needed to
  /// decrypt <c>wp_usermeta.api_keys</c> ciphertext written under the old WordPress-era scheme.
  /// </summary>
  public string CryptographyBaseUrl { get; set; } = null!;
}
