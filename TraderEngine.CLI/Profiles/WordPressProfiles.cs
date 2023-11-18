using AutoMapper;
using TraderEngine.CLI.DTOs.WordPress;
using TraderEngine.Common.DTOs.API.Request;

namespace TraderEngine.CLI.Profiles;

internal class WordPressProfiles : Profile
{
  public WordPressProfiles()
  {
    SourceMemberNamingConvention = LowerUnderscoreNamingConvention.Instance;
    DestinationMemberNamingConvention = PascalCaseNamingConvention.Instance;

    CreateMap<WordPressConfigDto, ConfigReqDto>().ReverseMap();
  }
}