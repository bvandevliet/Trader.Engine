using AutoMapper;
using TraderEngine.CLI.DTOs;
using TraderEngine.Common.DTOs.Request;
using TraderEngine.Common.DTOs.Response;

namespace TraderEngine.CLI.Profiles;

internal class CMCMarketCapProfile : Profile
{
  public CMCMarketCapProfile()
  {
    CreateMap<CMCAssetDto, MarketCapData>()
      .ForMember(
        dest => dest.Updated, opt => opt.MapFrom(
          src => src.last_updated))
      .ForMember(
        dest => dest.Market, opt => opt.MapFrom(
          src => new MarketDto(src.quote.FirstOrDefault().Key, src.symbol)))
      .ForMember(
        dest => dest.Price, opt => opt.MapFrom(
          src => src.quote.FirstOrDefault().Value.price))
      .ForMember(
        dest => dest.MarketCap, opt => opt.MapFrom(
          src => src.quote.FirstOrDefault().Value.market_cap));
  }
}