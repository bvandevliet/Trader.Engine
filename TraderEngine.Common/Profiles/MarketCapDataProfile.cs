using AutoMapper;
using TraderEngine.Common.DTOs.Database;
using TraderEngine.Common.DTOs.Response;

namespace TraderEngine.Common.Profiles;

internal class MarketCapDataProfile : Profile
{
  public MarketCapDataProfile()
  {
    CreateMap<MarketCapDataDto, MarketCapDataDb>()
      .ForMember(
        dest => dest.QuoteSymbol, opt => opt.MapFrom(
          src => src.Market.QuoteSymbol))
      .ForMember(
        dest => dest.BaseSymbol, opt => opt.MapFrom(
          src => src.Market.BaseSymbol))
      .ReverseMap();
  }
}