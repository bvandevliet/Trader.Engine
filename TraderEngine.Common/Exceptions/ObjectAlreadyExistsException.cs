namespace TraderEngine.Common.Exceptions;

/// <summary>
/// Thrown if the given object already exists in the current context.
/// </summary>
public class ObjectAlreadyExistsException : Exception
{
  public ObjectAlreadyExistsException() : base()
  {
  }

  public ObjectAlreadyExistsException(string? message) : base(message)
  {
  }
}