namespace TraderEngine.Common.Factories;

public interface INamedTypeFactory<T>
{
  public T? GetService(string name);
}
