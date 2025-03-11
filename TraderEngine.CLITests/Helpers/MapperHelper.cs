using AutoMapper;
using System.Reflection;

namespace TraderEngine.CLI.Helpers.Tests;

internal class MapperHelper
{
  public static IMapper CreateMapper()
  {
    IEnumerable<Assembly> assembliesToScan = AppDomain.CurrentDomain.GetAssemblies();

    assembliesToScan = new HashSet<Assembly>(assembliesToScan.Where((Assembly a) => !a.IsDynamic && a != typeof(Mapper).Assembly));

    var config = new MapperConfiguration(cfg => cfg.AddMaps(assembliesToScan));

    return config.CreateMapper();
  }
}

