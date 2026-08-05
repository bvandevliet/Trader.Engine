using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Data;
using TraderEngine.Data.Entities;
using TraderEngine.Data.Extensions;
using TraderEngine.Data.Repositories;
using TraderEngine.Migration.AppSettings;
using TraderEngine.Migration.Mappers;
using TraderEngine.Migration.Services;
using TraderEngine.Migration.WordPress;

namespace TraderEngine.Migration;

/// <summary>
/// One-shot migration from the legacy WordPress/MariaDB store to the current Postgres store.
/// Meant to run once, on demand, as its own short-lived container (see docker-compose.yml's
/// <c>migrate</c> profile) — never as a long-running service. Re-running is safe: users, config
/// and credentials already present (matched by email/user id) are left untouched.
/// </summary>
public class Program
{
  public static async Task Main(string[] args)
  {
    var builder = Host.CreateApplicationBuilder(args);
#if DEBUG
    builder.Configuration.AddJsonFile("appsettings.Private.json", optional: true, reloadOnChange: true);
#endif

    builder.Services.Configure<WordPressSettings>(builder.Configuration.GetSection("WordPress"));
    builder.Services.Configure<MigrationSettings>(builder.Configuration.GetSection("Migration"));

    builder.Services.AddDbContext<TraderEngineDbContext>(options => options
      .UseNpgsql(builder.Configuration.GetConnectionString("Postgres"))
      .UseSnakeCaseNamingConvention());

    builder.Services
      .AddIdentityCore<AppUser>(options => options.ConfigureTraderEngineIdentityPolicy())
      .AddEntityFrameworkStores<TraderEngineDbContext>();

    // Same key ring TraderEngine.API/.Web share, so credentials re-encrypted here are decryptable
    // by both going forward. Resolved identically to how those two hosts resolve it, so this
    // one-shot tool can't silently point at a different "secrets" folder than the long-running
    // hosts do.
    var keyRingPath = builder.Configuration.ResolveDataProtectionKeyRingPath(builder.Environment.ContentRootPath);
    builder.Services.AddSharedDataProtection(builder.Configuration, keyRingPath);

    builder.Services.AddScoped<IConfigRepository, EfConfigRepository>();
    builder.Services.AddScoped<IApiCredentialsRepository, EfApiCredentialsRepository>();

    builder.Services.AddHttpClient<CryptographyClient>((sp, httpClient) =>
    {
      var migrationSettings = sp.GetRequiredService<IOptions<MigrationSettings>>().Value;

      httpClient.BaseAddress = new(migrationSettings.CryptographyBaseUrl);
    });

    builder.Services.AddSingleton(sp => new WordPressReader(
      builder.Configuration.GetConnectionString("WordPress")
        ?? throw new InvalidOperationException("ConnectionStrings:WordPress must be configured."),
      sp.GetRequiredService<IOptions<WordPressSettings>>()));

    var host = builder.Build();

    using var scope = host.Services.CreateScope();
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    await services.GetRequiredService<TraderEngineDbContext>().Database.MigrateAsync();

    await RunMigration(services, logger);
  }

  private static async Task RunMigration(IServiceProvider services, ILogger logger)
  {
    var wordPressReader = services.GetRequiredService<WordPressReader>();
    var userManager = services.GetRequiredService<UserManager<AppUser>>();
    var configRepository = services.GetRequiredService<IConfigRepository>();
    var apiCredentialsRepository = services.GetRequiredService<IApiCredentialsRepository>();
    var cryptographyClient = services.GetRequiredService<CryptographyClient>();

    var wpUsers = await wordPressReader.GetAllUsers();
    var wpConfigs = await wordPressReader.GetAllConfigs();
    var wpApiKeys = await wordPressReader.GetAllEncryptedApiKeys();

    logger.LogInformation(
      "Loaded {UserCount} WordPress users, {ConfigCount} configs, {ApiKeyCount} API credential sets.",
      wpUsers.Count, wpConfigs.Count, wpApiKeys.Count);

    var migratedUsers = 0;
    var skippedUsers = 0;
    var migratedConfigs = 0;
    var migratedCredentials = 0;

    foreach (var (wpUserId, wpUser) in wpUsers)
    {
      var existing = await userManager.FindByEmailAsync(wpUser.user_email);

      Guid newUserId;

      if (existing != null)
      {
        logger.LogInformation(
          "User '{Email}' (WordPress ID {WpUserId}) already exists as {NewUserId}, skipping account creation.",
          wpUser.user_email, wpUserId, existing.Id);

        skippedUsers++;
        newUserId = existing.Id;
      }
      else
      {
        var newUser = new AppUser
        {
          UserName = wpUser.user_login,
          Email = wpUser.user_email,
          DisplayName = wpUser.display_name,
          // No password is set — the migrated account has no usable PasswordHash, so the only
          // way in is the "Forgot password" flow. Confirmed directly since the WordPress account
          // it originates from already verified this address.
          EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(newUser);

        if (!result.Succeeded)
        {
          logger.LogError(
            "Failed to create user for '{Email}' (WordPress ID {WpUserId}): {Errors}",
            wpUser.user_email, wpUserId, string.Join("; ", result.Errors.Select(e => e.Description)));

          continue;
        }

        logger.LogInformation(
          "Created user '{Email}' (WordPress ID {WpUserId}) as {NewUserId}.",
          wpUser.user_email, wpUserId, newUser.Id);

        migratedUsers++;
        newUserId = newUser.Id;
      }

      if (wpConfigs.TryGetValue(wpUserId, out var wpConfig))
      {
        var configDto = WordPressConfigMapper.Map(wpConfig);

        await configRepository.SaveConfig(newUserId, configDto);

        migratedConfigs++;
      }

      if (wpApiKeys.TryGetValue(wpUserId, out var encryptedKeys))
      {
        foreach (var wpExchangeName in ExchangeNamesIn(encryptedKeys))
        {
          if (!encryptedKeys.TryGetValue($"{wpExchangeName}_key", out var encryptedKey) ||
            !encryptedKeys.TryGetValue($"{wpExchangeName}_secret", out var encryptedSecret) ||
            string.IsNullOrEmpty(encryptedKey) || string.IsNullOrEmpty(encryptedSecret))
          {
            continue;
          }

          var exchangeName = CanonicalExchangeName(wpExchangeName, logger);

          var apiCred = new ApiCredReqDto
          {
            ApiKey = await cryptographyClient.Decrypt(encryptedKey),
            ApiSecret = await cryptographyClient.Decrypt(encryptedSecret),
          };

          await apiCredentialsRepository.SaveApiCred(newUserId, exchangeName, apiCred);

          migratedCredentials++;

          logger.LogInformation(
            "Migrated '{ExchangeName}' API credentials for {NewUserId} (WordPress ID {WpUserId}).",
            exchangeName, newUserId, wpUserId);
        }
      }
    }

    logger.LogInformation(
      "Migration complete: {MigratedUsers} users created, {SkippedUsers} already existed, " +
      "{MigratedConfigs} configs migrated, {MigratedCredentials} credential sets migrated.",
      migratedUsers, skippedUsers, migratedConfigs, migratedCredentials);
  }

  /// <summary>
  /// Distinct exchange name prefixes found in a WordPress <c>api_keys</c> blob — its keys are
  /// <c>{exchange}_key</c>/<c>{exchange}_secret</c> pairs, e.g. <c>bitvavo_key</c>.
  /// </summary>
  private static IEnumerable<string> ExchangeNamesIn(Dictionary<string, string> encryptedKeys)
  {
    return encryptedKeys.Keys
    .Where(key => key.EndsWith("_key", StringComparison.Ordinal))
    .Select(key => key[..^"_key".Length])
    .Distinct();
  }

  /// <summary>
  /// WordPress stored exchange names lowercase (<c>bitvavo</c>), but <see cref="ExchangeApiCredential.ExchangeName"/>
  /// is matched exactly (case-sensitively) against the app's hardcoded names — e.g.
  /// <c>AutomationOrchestrator</c>'s literal <c>"Bitvavo"</c>. A mismatched case here would
  /// silently make migrated credentials unfindable rather than fail loudly, so every known
  /// exchange is mapped explicitly; anything unrecognized falls back to capitalizing the first
  /// letter and is logged so it can be checked by hand.
  /// </summary>
  private static readonly Dictionary<string, string> _knownExchangeNames = new(StringComparer.OrdinalIgnoreCase)
  {
    ["bitvavo"] = "Bitvavo",
  };

  private static string CanonicalExchangeName(string wpExchangeName, ILogger logger)
  {
    if (_knownExchangeNames.TryGetValue(wpExchangeName, out var canonicalName))
    {
      return canonicalName;
    }

    logger.LogWarning(
      "Unrecognized exchange '{WpExchangeName}' in WordPress api_keys — falling back to capitalizing " +
      "it as-is; verify this matches the exchange name the app expects.", wpExchangeName);

    return string.Concat(wpExchangeName[..1].ToUpperInvariant(), wpExchangeName[1..]);
  }
}
