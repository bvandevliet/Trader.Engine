namespace TraderEngine.Common.Exchanges;

/// <summary>
/// Immutable exchange API credentials, threaded explicitly through calls rather than held as
/// mutable state on an <see cref="IExchange"/> instance.
/// </summary>
/// <param name="UserId">
/// This app's own internal user id, when known to the caller — purely for correlating logs (e.g.
/// a shared rate-limit throttle delay) back to a specific user without exposing any fragment of
/// <paramref name="ApiKey"/>/<paramref name="ApiSecret"/>. Not used for authentication or
/// authorization; left <see langword="null"/> wherever the caller has no notion of an app-level
/// user (e.g. tests constructing credentials directly).
/// </param>
public sealed record ExchangeCredentials(string ApiKey, string ApiSecret, Guid? UserId = null);
