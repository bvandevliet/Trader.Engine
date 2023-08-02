using Microsoft.Net.Http.Headers;
using TraderEngine.API.Exchanges;

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