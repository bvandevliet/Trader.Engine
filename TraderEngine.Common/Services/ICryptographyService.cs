namespace TraderEngine.Common.Services;

public interface ICryptographyService
{
  public Task<string> Decrypt(string cipherText);

  public Task<string> Encrypt(string plainText);
}