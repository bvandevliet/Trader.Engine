using Riok.Mapperly.Abstractions;
using TraderEngine.API.DTOs.CMC;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;

namespace TraderEngine.API.Mappers;

[Mapper]
public static partial class CliMapper
{
  // ── CMCAssetDto → MarketCapDataDto ───────────────────────────────────────────

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

  public static IEnumerable<MarketCapDataDto> MapCMCAssets(IEnumerable<CMCAssetDto> source)
  {
    return source.Select(MapCMCAsset);
  }
}
