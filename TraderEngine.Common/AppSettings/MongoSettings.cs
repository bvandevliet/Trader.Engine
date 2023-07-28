namespace TraderEngine.Common.AppSettings;

public class MongoSettings
{
  public string ConnectionString { get; set; } = "mongodb://dev:dev@localhost:27017";

  public MongoDatabases Databases { get; set; } = new();

  public class MongoDatabases
  {
    public MongoUserData UserData { get; set; } = new();

    public class MongoUserData
    {
      public string Users { get; set; } = "Users";

      public string Configurations { get; set; } = "Configurations";
    }

    public MongoMetricData MetricData { get; set; } = new();

    public class MongoMetricData
    {
      public string MarketCap { get; set; } = "MarketCap";
    }
  }
}