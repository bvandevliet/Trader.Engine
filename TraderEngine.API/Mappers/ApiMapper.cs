using System.Globalization;
using System.Text.Json;
using Riok.Mapperly.Abstractions;
using TraderEngine.API.DTOs.Bitvavo.Request;
using TraderEngine.API.DTOs.Bitvavo.Response;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Enums;

namespace TraderEngine.API.Mappers;

[Mapper]
public partial class ApiMapper : IApiMapper
{
  // ── BitvavoMarketDataDto → MarketDataDto ─────────────────────────────────────

  [MapProperty(nameof(BitvavoMarketDataDto.MinOrderInQuoteAsset), nameof(MarketDataDto.MinOrderSizeInQuote))]
  [MapProperty(nameof(BitvavoMarketDataDto.MinOrderInBaseAsset), nameof(MarketDataDto.MinOrderSizeInBase))]
  public partial MarketDataDto MapMarketData(BitvavoMarketDataDto source);

  // ── BitvavoAssetDataDto → AssetDataDto ───────────────────────────────────────

  [MapProperty(nameof(BitvavoAssetDataDto.Symbol), nameof(AssetDataDto.BaseSymbol))]
  public partial AssetDataDto MapAssetData(BitvavoAssetDataDto source);

  // ── OrderReqDto → BitvavoOrderReqDto ────────────────────────────────────────

  [MapProperty(nameof(OrderReqDto.Type), nameof(BitvavoOrderReqDto.OrderType))]
  [MapperIgnoreTarget(nameof(BitvavoOrderReqDto.OperatorId))]
  [MapperIgnoreTarget(nameof(BitvavoOrderReqDto.ResponseRequired))]
  [MapperIgnoreTarget(nameof(BitvavoOrderReqDto.TimeInForce))]
  [MapperIgnoreTarget(nameof(BitvavoOrderReqDto.DisableMarketProtection))]
  public partial BitvavoOrderReqDto MapOrderReq(OrderReqDto source);

  // ── BitvavoOrderDto → OrderDto ───────────────────────────────────────────────

  [MapProperty(nameof(BitvavoOrderDto.OrderId), nameof(OrderDto.Id))]
  [MapProperty(nameof(BitvavoOrderDto.FilledAmount), nameof(OrderDto.AmountFilled))]
  [MapProperty(nameof(BitvavoOrderDto.FilledAmountQuote), nameof(OrderDto.AmountQuoteFilled))]
  [MapProperty(nameof(BitvavoOrderDto.OrderType), nameof(OrderDto.Type))]
  public partial OrderDto MapOrder(BitvavoOrderDto source);

  public IEnumerable<OrderDto> MapOrders(IEnumerable<BitvavoOrderDto> source)
  {
    return source.Select(MapOrder);
  }

  // ── Type converters ──────────────────────────────────────────────────────────

  // MarketReqDto → string  (e.g. "BTC-EUR")
  private static string MapMarket(MarketReqDto market)
  {
    return market.ToString();
  }

  // string → MarketReqDto  (e.g. "BTC-EUR" → new MarketReqDto("EUR","BTC"))
  private static MarketReqDto ParseMarket(string market)
  {
    var parts = market.Split('-', StringSplitOptions.TrimEntries);
    return new MarketReqDto(parts[1], parts[0]);
  }

  // OrderSide enum → camelCase string  ("buy" / "sell")
  private static string MapOrderSide(OrderSide side)
  {
    return JsonNamingPolicy.CamelCase.ConvertName(side.ToString());
  }

  // OrderType enum → camelCase string  ("market" / "limit")
  private static string MapOrderType(OrderType orderType)
  {
    return JsonNamingPolicy.CamelCase.ConvertName(orderType.ToString());
  }

  // string → OrderSide  (case-insensitive; unknown → default)
  private static OrderSide ParseOrderSide(string value)
  {
    return Enum.TryParse<OrderSide>(value, ignoreCase: true, out var result) ? result : default;
  }

  // string → OrderType  (case-insensitive; unknown → default)
  private static OrderType ParseOrderType(string value)
  {
    return Enum.TryParse<OrderType>(value, ignoreCase: true, out var result) ? result : default;
  }

  // string → OrderStatus  (case-insensitive; unknown → default, matching AutoMapper behaviour)
  private static OrderStatus ParseOrderStatus(string value)
  {
    return Enum.TryParse<OrderStatus>(value, ignoreCase: true, out var result) ? result : default;
  }

  // string → MarketStatus  (case-insensitive; unknown → default)
  private static MarketStatus ParseMarketStatus(string value)
  {
    return Enum.TryParse<MarketStatus>(value, ignoreCase: true, out var result) ? result : default;
  }

  // string? → decimal  (invariant culture; returns 0 for null, matching AutoMapper default behaviour)
  private static decimal ParseDecimal(string? value)
  {
    return value is null ? 0m : decimal.Parse(value, CultureInfo.InvariantCulture);
  }

  // decimal? → string?  (invariant culture)
  private static string? FormatDecimal(decimal? value)
  {
    return value?.ToString(CultureInfo.InvariantCulture);
  }
}
