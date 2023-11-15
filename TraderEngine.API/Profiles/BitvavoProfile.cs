using AutoMapper;
using TraderEngine.API.DTOs.Bitvavo.Request;
using TraderEngine.API.DTOs.Bitvavo.Response;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;

namespace TraderEngine.API.Profiles;

public class BitvavoProfile : Profile
{
  public BitvavoProfile()
  {
    CreateMap<BitvavoMarketDataDto, MarketDataDto>()
      .ForMember(
        dest => dest.MinOrderSizeInQuote, opt => opt.MapFrom(
          src => src.MinOrderInQuoteAsset))
      .ForMember(
        dest => dest.MinOrderSizeInBase, opt => opt.MapFrom(
          src => src.MinOrderInBaseAsset));

    CreateMap<OrderReqDto, BitvavoOrderNewReqDto>();
    CreateMap<BitvavoOrderDto, OrderDto>();
  }
}