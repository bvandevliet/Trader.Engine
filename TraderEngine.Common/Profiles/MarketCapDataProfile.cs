using AutoMapper;
using System.Text.Json;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.DTOs.Database;

namespace TraderEngine.Common.Profiles;

internal class MarketCapDataProfile : Profile
{
  public MarketCapDataProfile()
  {
    var jsonOptions = new JsonSerializerOptions()
    {
      PropertyNamingPolicy = null,
      PropertyNameCaseInsensitive = true,
    };

    CreateMap<MarketCapDataDto, MarketCapDataDb>()
      .ForMember(
        dest => dest.QuoteSymbol, opt => opt.MapFrom(
          src => src.Market.QuoteSymbol))
      .ForMember(
        dest => dest.BaseSymbol, opt => opt.MapFrom(
          src => src.Market.BaseSymbol))
      .ForMember(
        dest => dest.Tags, opt => opt.MapFrom(
          src => JsonSerializer.Serialize(src.Tags, jsonOptions)))
      .ReverseMap()
      .ForMember(
        dest => dest.Tags, opt => opt.MapFrom(
          src => JsonSerializer.Deserialize<List<string>>(src.Tags ?? "[]", jsonOptions)));
  }
}