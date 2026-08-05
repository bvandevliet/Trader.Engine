namespace TraderEngine.Common.Exchanges;

/// <summary>
/// Immutable exchange API credentials, threaded explicitly through calls rather than held as
/// mutable state on an <see cref="IExchange"/> instance.
/// </summary>
public sealed record ExchangeCredentials(string ApiKey, string ApiSecret);
