namespace TraderEngine.Common.Exceptions;

/// <summary>
/// Thrown if the given object is invalid in the current context.
/// </summary>
public class InvalidObjectException : Exception
{
  public InvalidObjectException() : base()
  {
  }

  public InvalidObjectException(string? message) : base(message)
  {
  }
}