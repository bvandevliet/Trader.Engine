using Microsoft.Extensions.DependencyInjection;
using TraderEngine.Data.Repositories;
using TraderEngine.Data.Services;

namespace TraderEngine.Data.Extensions;

public static class ServiceCollectionExtensions
{
  /// <summary>
  /// Registers the scoped services/repositories both TraderEngine.API and TraderEngine.Web
  /// depend on identically — JWT minting/validation and the config/exchange-credential
  /// repositories both hosts read from and write to the one shared database.
  /// </summary>
  public static IServiceCollection AddTraderEngineSharedServices(this IServiceCollection services)
  {
    return services
      .AddScoped<IJwtTokenService, JwtTokenService>()
      .AddScoped<IConfigRepository, EfConfigRepository>()
      .AddScoped<IApiCredentialsRepository, EfApiCredentialsRepository>();
  }
}
