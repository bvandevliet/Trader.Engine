using Riok.Mapperly.Abstractions;
using TraderEngine.API.DTOs.CMC;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;

namespace TraderEngine.API.Mappers;

[Mapper]
public static partial class CliMapper
{
  // ── CMCAssetDto → MarketCapDataDto ───────────────────────────────────────────

  // Hand-written: picks an arbitrary single entry out of the Quote dictionary (business logic, not
  // structural mapping) and constructs the nested Market via MarketReqDto's constructor, which
  // Mapperly's property-mapping attributes can't express.
  public static MarketCapDataDto MapCMCAsset(CMCAssetDto source)
  {
    var firstQuote = source.Quote.FirstOrDefault();
    return new MarketCapDataDto
    {
      Market = new MarketReqDto(firstQuote.Key, source.Symbol),
      Price = (double)firstQuote.Value.Price,
      MarketCap = (double)firstQuote.Value.Market_Cap,
      Tags = source.Tags.ToList(),
      Updated = source.Last_Updated,
    };
  }

  public static partial IEnumerable<MarketCapDataDto> MapCMCAssets(IEnumerable<CMCAssetDto> source);
}
