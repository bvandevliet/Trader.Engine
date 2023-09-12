namespace TraderEngine.Common.Helpers;

public class NamedTypeHelper<T>
{
  public string Name { get; }
  public T Value { get; }

  public NamedTypeHelper(string name, T value)
  {
    Name = name;
    Value = value;
  }
}
