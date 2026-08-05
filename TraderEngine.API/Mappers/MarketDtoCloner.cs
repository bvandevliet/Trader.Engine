using Riok.Mapperly.Abstractions;
using TraderEngine.Common.DTOs.API.Request;

namespace TraderEngine.API.Mappers;

[Mapper(UseDeepCloning = true)]
public static partial class MarketDtoCloner
{
  public static partial MarketReqDto DeepClone(this MarketReqDto t);
}
