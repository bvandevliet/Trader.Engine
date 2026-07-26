using System.Net.Http.Json;

namespace TraderEngine.Migration.Services;

/// <summary>
/// Talks to the legacy <c>traderengine.cryptography</c> microservice — the only thing left that
/// can decrypt <c>wp_usermeta.api_keys</c> ciphertext produced by the old WordPress-era
/// encryption scheme. Used exclusively during migration to recover plaintext exchange API
/// credentials before re-encrypting them with the new Data Protection key ring
/// (<see cref="Repositories.TargetStore"/>); never used going forward.
/// </summary>
public class CryptographyClient
{
  private readonly HttpClient _httpClient;

  public CryptographyClient(HttpClient httpClient)
  {
    _httpClient = httpClient;
  }

  public async Task<string> Decrypt(string cipherText)
  {
    using var response = await _httpClient.PostAsJsonAsync("api/decrypt", cipherText);

    response.EnsureSuccessStatusCode();

    return await response.Content.ReadAsStringAsync();
  }
}
