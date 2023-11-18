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

    CreateMap<OrderReqDto, BitvavoOrderReqDto>()
      .ForMember(
        dest => dest.Market, opt => opt.MapFrom(
          src => $"{src.Market.BaseSymbol}-{src.Market.QuoteSymbol}"))
      .ForMember(
        dest => dest.Side, opt => opt.MapFrom(
          src => src.Side))
      .ForMember(
        dest => dest.OrderType, opt => opt.MapFrom(
          src => src.Type));

    CreateMap<BitvavoOrderDto, OrderDto>()
      .ForMember(
        dest => dest.Id, opt => opt.MapFrom(
          src => src.OrderId))
      .ForMember(
        dest => dest.Market, opt => opt.MapFrom(
          src => new MarketReqDto(
            src.Market.Split('-', StringSplitOptions.TrimEntries)[1],
            src.Market.Split('-', StringSplitOptions.TrimEntries)[0])))
      .ForMember(
        dest => dest.AmountFilled, opt => opt.MapFrom(
          src => src.FilledAmount))
      .ForMember(
        dest => dest.AmountQuoteFilled, opt => opt.MapFrom(
          src => src.FilledAmountQuote));
    //.ForMember(
    //  dest => dest.Created, opt => opt.MapFrom(
    //    src => new DateTime((int)src.Created!)))
    //.ForMember(
    //  dest => dest.Updated, opt => opt.MapFrom(
    //    src => new DateTime((int)src.Updated!)));
  }
}