using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using TraderEngine.Common.DTOs.Response;

namespace TraderEngine.Common.DTOs.Database;

public class MarketCapDataDb : MarketCapDataDto
{
  [BsonId]
  public BsonObjectId? Id { get; set; }
}