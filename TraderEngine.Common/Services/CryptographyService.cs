using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace TraderEngine.Common.Services;

public class CryptographyService : ICryptographyService
{
  private readonly ILogger<CryptographyService> _logger;
  private readonly HttpClient _httpClient;

  public CryptographyService(
    ILogger<CryptographyService> logger,
    HttpClient httpClient)
  {
    _logger = logger;
    _httpClient = httpClient;
  }

  public async Task<string> Encrypt(string plainText)
  {
    using var response = await _httpClient.PostAsJsonAsync("api/encrypt", plainText);

    try
    {
      response.EnsureSuccessStatusCode();
    }
    catch (Exception)
    {
      throw;
    }

    return await response.Content.ReadAsStringAsync();
  }

  public async Task<string> Decrypt(string cipherText)
  {
    using var response = await _httpClient.PostAsJsonAsync("api/decrypt", cipherText);

    try
    {
      response.EnsureSuccessStatusCode();
    }
    catch (Exception)
    {
      throw;
    }

    return await response.Content.ReadAsStringAsync();
  }
}
