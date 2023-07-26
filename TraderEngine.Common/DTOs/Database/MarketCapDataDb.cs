using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using TraderEngine.Common.DTOs.Response;

namespace TraderEngine.Common.DTOs.Database;

public class MarketCapDataDb : MarketCapData
{
  [BsonId]
  public BsonObjectId? Id { get; set; }
}