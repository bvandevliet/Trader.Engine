using System.Security.Cryptography;
using System.Text;

namespace TraderEngine.API.Exchanges;

/// <summary>
/// Computes the HMAC-SHA256 request signature shared by Bitvavo's REST and WebSocket v2 APIs.
/// </summary>
internal static class BitvavoSignature
{
  public static string Compute(string apiSecret, long timestamp, string method, string path, string? payload)
  {
    var message = new StringBuilder().Append(timestamp).Append(method).Append(path);

    if (payload != null)
      message.Append(payload);

    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiSecret));

    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message.ToString()));

    return Convert.ToHexString(hash).ToLowerInvariant();
  }
}
