using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Exchanges;
using TraderEngine.Common.Models;

namespace TraderEngine.Common.Tests.Exchanges;

/// <summary>
/// A <see cref="MockExchange"/> whose <see cref="GetAsset"/> reports no known decimals
/// precision (<c>Decimals = null</c>), unlike <see cref="MockExchange"/> which always reports
/// 8. Used to exercise the dust-liquidation branch in <c>RebalancingService</c> that falls
/// back to the unrounded allocation amount when precision is unknown. Uses the same
/// base-class-hiding pattern as the production <see cref="SimExchange"/>.
/// </summary>
internal sealed class NullDecimalsExchange : MockExchange, IExchange
{
  public NullDecimalsExchange(
    string quoteSymbol, decimal minOrderSize, decimal makerFee, decimal takerFee, Balance curBalance)
    : base(quoteSymbol, minOrderSize, makerFee, takerFee, curBalance)
  {
  }

  public new Task<AssetDataDto?> GetAsset(string baseSymbol)
  {
    return Task.FromResult<AssetDataDto?>(new AssetDataDto
    {
      BaseSymbol = baseSymbol,
      Name = baseSymbol,
      Decimals = null,
    });
  }
}
