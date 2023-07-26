using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TraderEngine.Common.DTOs.Database;

public class ConfigDto : Request.ConfigDto
{
  [BsonId]
  public BsonObjectId? Id { get; set; }
}