using System.Reflection;
using AutoMapper;

namespace TraderEngine.CLI.Tests.Helpers;

internal class MapperHelper
{
  public static IMapper CreateMapper()
  {
    IEnumerable<Assembly> assembliesToScan = AppDomain.CurrentDomain.GetAssemblies();

    assembliesToScan = new HashSet<Assembly>(assembliesToScan.Where((a) => !a.IsDynamic && a != typeof(Mapper).Assembly));

    var config = new MapperConfiguration(cfg => cfg.AddMaps(assembliesToScan));

    return config.CreateMapper();
  }
}

