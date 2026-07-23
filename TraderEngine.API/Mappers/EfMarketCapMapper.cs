using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Data.Entities;

namespace TraderEngine.API.Mappers;

public static class EfMarketCapMapper
{
  public static MarketCapMetric MapToEntity(MarketCapDataDto source)
  {
    return new()
    {
      QuoteSymbol = source.Market.QuoteSymbol,
      BaseSymbol = source.Market.BaseSymbol,
      Price = source.Price,
      MarketCap = source.MarketCap,
      Tags = source.Tags,
      Updated = source.Updated,
    };
  }

  public static MarketCapDataDto MapFromEntity(MarketCapMetric source)
  {
    return new()
    {
      Market = new MarketReqDto(source.QuoteSymbol, source.BaseSymbol),
      Price = source.Price,
      MarketCap = source.MarketCap,
      Tags = source.Tags,
      Updated = source.Updated,
    };
  }

  public static IEnumerable<MarketCapDataDto> MapFromEntities(IEnumerable<MarketCapMetric> source)
  {
    return source.Select(MapFromEntity);
  }
}
