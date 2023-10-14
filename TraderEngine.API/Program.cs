using Microsoft.AspNetCore.Diagnostics;
using TraderEngine.API.Exchanges;
using TraderEngine.API.Factories;
using TraderEngine.Common.Extensions;
using TraderEngine.Common.Factories;
using TraderEngine.Common.Repositories;
using TraderEngine.Common.Services;

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

    builder.Services.AddRouting(options =>
    {
      options.LowercaseUrls = true;
    });

    builder.Services.AddControllers();

    // Swagger/OpenAPI.
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

    builder.Services.AddScoped<SqlConnectionFactory>();

    builder.Services.AddScoped<IMarketCapInternalRepository, MarketCapInternalRepository>();

    builder.Services.AddScoped<IMarketCapService, MarketCapService>();

    builder.Services.AddHttpClient<IExchange>().ApplyDefaultPoolAndPolicyConfig();

    foreach (Type exchange in _exchanges) { builder.Services.AddScoped(exchange); }

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