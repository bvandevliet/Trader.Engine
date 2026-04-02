using TraderEngine.CLI.DTOs.CMC;
using TraderEngine.CLI.DTOs.WordPress;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;

namespace TraderEngine.CLI.Mappers;

public interface ICliMapper
{
  public MarketCapDataDto MapCMCAsset(CMCAssetDto source);

  public IEnumerable<MarketCapDataDto> MapCMCAssets(IEnumerable<CMCAssetDto> source);

  public ConfigReqDto MapConfig(WordPressConfigDto source);

  public WordPressConfigDto MapConfigReverse(ConfigReqDto source);
}
