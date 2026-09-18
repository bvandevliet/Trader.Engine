using TraderEngine.Common.Enums;
using TraderEngine.Common.Results;

namespace TraderEngine.Common.Exchanges;

/// <summary>
/// Optional capability for <see cref="IExchange"/> implementations that can report just the
/// available quote-currency (<see cref="IExchange.QuoteSymbol"/>) balance without also pricing
/// every other held asset. A full <see cref="IExchange.GetBalance"/> call commonly fetches a live
/// price for every distinct held asset to build a complete <see cref="Models.Balance"/>, work that's
/// wasted whenever a caller only needs the quote-currency total, such as seeding
/// <see cref="Services.BudgetLedger"/> or its mid-run reconciliation fetch. When an exchange
/// implements this, <see cref="Services.RebalancingService"/> uses it instead of <see cref="IExchange.GetBalance"/>
/// for those quote-only reads, falling back to a full <see cref="IExchange.GetBalance"/> call if
/// the exchange doesn't implement it or the lightweight call itself fails.
/// </summary>
public interface IExchangeQuoteBalance
{
  /// <summary>
  /// Fetches just the total quote-currency balance (available plus whatever's currently reserved
  /// in open orders, matching <see cref="Models.Balance.AmountQuoteAvailable"/>'s own definition),
  /// with no per-asset price lookups.
  /// </summary>
  public Task<Result<decimal, ExchangeErrCodeEnum>> GetQuoteBalanceAvailable(ExchangeCredentials credentials);
}
