namespace TraderEngine.Common.Services;

public interface ICryptographyService
{
  Task<string> Decrypt(string cipherText);

  Task<string> Encrypt(string plainText);
}