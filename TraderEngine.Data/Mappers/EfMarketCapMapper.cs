using Riok.Mapperly.Abstractions;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Data.Entities;

namespace TraderEngine.Data.Mappers;

[Mapper]
public static partial class EfMarketCapMapper
{
  [MapProperty($"{nameof(MarketCapDataDto.Market)}.{nameof(MarketReqDto.QuoteSymbol)}", nameof(MarketCapMetric.QuoteSymbol))]
  [MapProperty($"{nameof(MarketCapDataDto.Market)}.{nameof(MarketReqDto.BaseSymbol)}", nameof(MarketCapMetric.BaseSymbol))]
  public static partial MarketCapMetric MapToEntity(MarketCapDataDto source);

  // Hand-written: MarketReqDto's flat source fields must go through its constructor (which
  // upper-cases both), which Mapperly's unflattening can't express — it only assigns directly into
  // an existing nested instance, and MarketCapDataDto.Market has no default instance to assign into
  // (defaults to null!), which crashed with a NullReferenceException when generated.
  public static MarketCapDataDto MapFromEntity(MarketCapMetric source)
  {
    return new()
    {
      Market = new MarketReqDto(source.QuoteSymbol, source.BaseSymbol),
      Name = source.Name,
      Price = source.Price,
      MarketCap = source.MarketCap,
      Tags = source.Tags,
      Updated = source.Updated,
    };
  }

  public static partial IEnumerable<MarketCapDataDto> MapFromEntities(IEnumerable<MarketCapMetric> source);
}
