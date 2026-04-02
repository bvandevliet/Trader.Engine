using TraderEngine.API.DTOs.Bitvavo.Request;
using TraderEngine.API.DTOs.Bitvavo.Response;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;

namespace TraderEngine.API.Mappers;

public interface IApiMapper
{
  public MarketDataDto MapMarketData(BitvavoMarketDataDto source);

  public AssetDataDto MapAssetData(BitvavoAssetDataDto source);

  public BitvavoOrderReqDto MapOrderReq(OrderReqDto source);

  public OrderDto MapOrder(BitvavoOrderDto source);

  public IEnumerable<OrderDto> MapOrders(IEnumerable<BitvavoOrderDto> source);
}
