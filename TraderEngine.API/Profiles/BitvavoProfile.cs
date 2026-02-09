using System.Text.Json;
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
    _ = CreateMap<BitvavoMarketDataDto, MarketDataDto>()
      .ForMember(
        dest => dest.MinOrderSizeInQuote, opt => opt.MapFrom(
          src => src.MinOrderInQuoteAsset))
      .ForMember(
        dest => dest.MinOrderSizeInBase, opt => opt.MapFrom(
          src => src.MinOrderInBaseAsset));

    _ = CreateMap<BitvavoAssetDataDto, AssetDataDto>()
      .ForMember(
        dest => dest.BaseSymbol, opt => opt.MapFrom(
          src => src.Symbol));

    _ = CreateMap<OrderReqDto, BitvavoOrderReqDto>()
      .ForMember(
        dest => dest.Market, opt => opt.MapFrom(
          src => $"{src.Market}"))
      .ForMember(
        dest => dest.Side, opt => opt.MapFrom(
          src => JsonNamingPolicy.CamelCase.ConvertName(src.Side.ToString())))
      .ForMember(
        dest => dest.OrderType, opt => opt.MapFrom(
          src => JsonNamingPolicy.CamelCase.ConvertName(src.Type.ToString())));

    _ = CreateMap<BitvavoOrderDto, OrderDto>()
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