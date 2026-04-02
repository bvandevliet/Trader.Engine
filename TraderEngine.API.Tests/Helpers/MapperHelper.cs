using TraderEngine.API.Mappers;

namespace TraderEngine.API.Tests.Helpers;

internal static class MapperHelper
{
  public static IApiMapper CreateApiMapper()
  {
    return new ApiMapper();
  }
}

