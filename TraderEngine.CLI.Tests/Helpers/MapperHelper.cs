using TraderEngine.CLI.Mappers;

namespace TraderEngine.CLI.Tests.Helpers;

internal static class MapperHelper
{
  public static ICliMapper CreateCliMapper()
  {
    return new CliMapper();
  }
}

