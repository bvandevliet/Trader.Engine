namespace TraderEngine.Web.Services;

/// <summary>
/// Thrown by <see cref="IDelegatedAccessResolver"/> when a caller tries to act on another user's
/// account (via an <c>actingAsClientId</c> query parameter) without a currently active delegation
/// grant for that exact pair — lets every call site show a specific, actionable message instead of
/// letting the request fall through to whatever that user's own data would have shown.
/// </summary>
public class DelegationAccessDeniedException : Exception
{
  public DelegationAccessDeniedException(string message) : base(message)
  {
  }
}
