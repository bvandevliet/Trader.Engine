using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TraderEngine.Common.DTOs.Database;

public class MarketCapDto : Response.MarketCapDto
{
  [BsonId]
  public BsonObjectId? Id { get; set; }
}