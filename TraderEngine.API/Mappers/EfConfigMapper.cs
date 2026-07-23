using Riok.Mapperly.Abstractions;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Data.Entities;

namespace TraderEngine.API.Mappers;

[Mapper]
public static partial class EfConfigMapper
{
  [MapperIgnoreSource(nameof(RebalancingConfiguration.UserId))]
  [MapperIgnoreSource(nameof(RebalancingConfiguration.User))]
  public static partial ConfigReqDto MapConfig(RebalancingConfiguration source);

  [MapperIgnoreTarget(nameof(RebalancingConfiguration.UserId))]
  [MapperIgnoreTarget(nameof(RebalancingConfiguration.User))]
  public static partial void MapConfigReverse(ConfigReqDto source, RebalancingConfiguration target);
}
