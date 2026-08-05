using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TraderEngine.Data.AppSettings;

namespace TraderEngine.Data.Extensions;

public static class JwtSettingsExtensions
{
  // RFC 7518 §3.2 requires an HMAC SHA-256 signing key of at least 256 bits; anything shorter
  // is accepted by SymmetricSecurityKey without complaint, it just makes the signature easier
  // to brute-force.
  private const int MinimumSigningKeyBytes = 32;

  /// <summary>
  /// Fails fast at startup — in both TraderEngine.API (which validates tokens) and
  /// TraderEngine.Web (which mints them) — rather than silently accepting an empty or
  /// too-short <see cref="JwtSettings.SigningKey"/> that would only surface as a hard-to-trace
  /// signature/validation failure (or a trivially forgeable token) at request time.
  /// </summary>
  public static void ValidateSigningKey(this JwtSettings jwtSettings)
  {
    if (Encoding.UTF8.GetByteCount(jwtSettings.SigningKey) < MinimumSigningKeyBytes)
      throw new InvalidOperationException(
        $"Jwt:SigningKey must be configured with at least {MinimumSigningKeyBytes} bytes (256 bits) " +
        "for HMACSHA256 — set it via the Jwt:SigningKey setting or JWT_SIGNING_KEY environment variable.");
  }

  /// <summary>
  /// Binds the "Jwt" configuration section, fails fast via <see cref="ValidateSigningKey"/>, and
  /// registers it for <c>IOptions&lt;JwtSettings&gt;</c> injection — shared identically by
  /// TraderEngine.API and TraderEngine.Web so both hosts validate and bind the one signing key
  /// they mint/verify each other's tokens with the same way. Returns the bound settings for hosts
  /// (TraderEngine.API) that also need them synchronously, e.g. to configure
  /// <c>AddJwtBearer</c>'s <c>TokenValidationParameters</c> before the container is built.
  /// </summary>
  public static JwtSettings AddTraderEngineJwtSettings(this IServiceCollection services, IConfiguration configuration)
  {
    var jwtSettingsSection = configuration.GetSection("Jwt");
    var jwtSettings = jwtSettingsSection.Get<JwtSettings>() ?? new JwtSettings();
    jwtSettings.ValidateSigningKey();

    services.Configure<JwtSettings>(jwtSettingsSection);

    return jwtSettings;
  }
}
