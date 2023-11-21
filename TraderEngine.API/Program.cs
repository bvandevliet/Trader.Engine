using MySqlConnector;
using TraderEngine.API.Exchanges;
using TraderEngine.API.Factories;
using TraderEngine.API.Services;
using TraderEngine.Common.Extensions;
using TraderEngine.Common.Factories;
using TraderEngine.Common.Repositories;

namespace TraderEngine.API;

public class Program
{
  private static readonly List<Type> _exchanges = new()
  {
    typeof(BitvavoExchange),
  };

  public static void Main(string[] args)
  {
    var builder = WebApplication.CreateBuilder(args);
#if DEBUG
    // Add private appsettings.json file when debugging.
    builder.Configuration.AddJsonFile("appsettings.Private.json", optional: true, reloadOnChange: true);
#endif

    builder.Services.AddRouting(options =>
    {
      options.LowercaseUrls = true;
    });

    builder.Services.AddControllers();

    // Swagger/OpenAPI.
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

    builder.Services.AddScoped<INamedTypeFactory<MySqlConnection>, SqlConnectionFactory>();

    builder.Services.AddScoped<IMarketCapInternalRepository, MarketCapInternalRepository>();

    builder.Services.AddScoped<IMarketCapService, MarketCapService>();

    builder.Services.AddHttpClient<IExchange>().ApplyDefaultPoolAndPolicyConfig();

    foreach (var exchangeType in _exchanges) { builder.Services.AddScoped(exchangeType); }

    builder.Services.AddScoped(x => new ExchangeFactory(x, _exchanges));

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
      app.UseSwagger();
      app.UseSwaggerUI();
    }

    //app.UseAuthorization();

    app.MapControllers();

    app.Run();
  }
}