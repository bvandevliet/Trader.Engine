namespace TraderEngine.Common.Factories;

public interface INamedTypeFactory<T>
{
  T GetService(string name);
}
