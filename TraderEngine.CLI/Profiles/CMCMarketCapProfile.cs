using AutoMapper;
using TraderEngine.CLI.DTOs.CMC;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;

namespace TraderEngine.CLI.Profiles;

internal class CMCMarketCapProfile : Profile
{
  public CMCMarketCapProfile()
  {
    SourceMemberNamingConvention = LowerUnderscoreNamingConvention.Instance;
    DestinationMemberNamingConvention = PascalCaseNamingConvention.Instance;

    _ = CreateMap<CMCAssetDto, MarketCapDataDto>()
      .ForMember(
        dest => dest.Updated, opt => opt.MapFrom(
          src => src.Last_Updated))
      .ForMember(
        dest => dest.Market, opt => opt.MapFrom(
          src => new MarketReqDto(src.Quote.FirstOrDefault().Key, src.Symbol)))
      .ForMember(
        dest => dest.Price, opt => opt.MapFrom(
          src => src.Quote.FirstOrDefault().Value.Price))
      .ForMember(
        dest => dest.MarketCap, opt => opt.MapFrom(
          src => src.Quote.FirstOrDefault().Value.Market_Cap));
  }
}