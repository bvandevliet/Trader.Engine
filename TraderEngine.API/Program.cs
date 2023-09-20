using MySqlConnector;
using TraderEngine.API.Exchanges;
using TraderEngine.Common.Bootstrap;

namespace TraderEngine.API;

public class Program
{
  public static void Main(string[] args)
  {
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddControllers();

    // Swagger/OpenAPI.
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

    builder.Services.AddTransient(x =>
    {
      var dbConnection = new MySqlConnection(new(builder.Configuration.GetConnectionString("MySql")));

      dbConnection.Initialize().GetAwaiter().GetResult();

      return dbConnection;
    });

    builder.Services.AddHttpClient<BitvavoExchange>(httpClient =>
    {
      httpClient.BaseAddress = new("https://api.bitvavo.com/v2/");
    });

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