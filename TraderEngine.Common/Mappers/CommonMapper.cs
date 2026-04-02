using System.Text.Json;
using Riok.Mapperly.Abstractions;
using TraderEngine.Common.DTOs.API.Request;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.DTOs.Database;
using TraderEngine.Common.Models;

namespace TraderEngine.Common.Mappers;

[Mapper]
public partial class CommonMapper : ICommonMapper
{
  private static readonly JsonSerializerOptions _jsonOptions = new()
  {
    PropertyNamingPolicy = null,
    PropertyNameCaseInsensitive = true,
  };

  // ── Allocation ──────────────────────────────────────────────────────────────

  public partial AllocationDto MapAllocation(Allocation source);

  // ── Balance ──────────────────────────────────────────────────────────────────

  public BalanceDto MapBalance(Balance source)
  {
    return new()
    {
      QuoteSymbol = source.QuoteSymbol,
      AmountQuoteAvailable = source.AmountQuoteAvailable,
      AmountQuoteTotal = source.AmountQuoteTotal,
      Allocations = source.Allocations
      .OrderBy(a => !a.Market.BaseSymbol.Equals(source.QuoteSymbol))
      .ThenByDescending(a => a.AmountQuote)
      .Select(MapAllocation)
      .ToList(),
    };
  }

  // ── MarketCapDataDto → MarketCapDataDb ───────────────────────────────────────

  public MarketCapDataDb MarketCapToDb(MarketCapDataDto source)
  {
    return new()
    {
      QuoteSymbol = source.Market.QuoteSymbol,
      BaseSymbol = source.Market.BaseSymbol,
      Price = source.Price,
      MarketCap = source.MarketCap,
      Tags = SerializeTags(source.Tags),
      Updated = source.Updated,
    };
  }

  // ── MarketCapDataDb → MarketCapDataDto ───────────────────────────────────────

  public MarketCapDataDto MarketCapFromDb(MarketCapDataDb source)
  {
    return new()
    {
      Market = new MarketReqDto(source.QuoteSymbol, source.BaseSymbol),
      Price = source.Price,
      MarketCap = source.MarketCap,
      Tags = DeserializeTags(source.Tags),
      Updated = source.Updated,
    };
  }

  public IEnumerable<MarketCapDataDto> MarketCapsFromDb(IEnumerable<MarketCapDataDb> source)
  {
    return source.Select(MarketCapFromDb);
  }

  // ── Type converters ──────────────────────────────────────────────────────────

  // List<string> → string (serialize Tags for DB storage)
  private static string SerializeTags(List<string> tags)
  {
    return JsonSerializer.Serialize(tags, _jsonOptions);
  }

  // string? → List<string> (deserialize Tags from DB storage)
  private static List<string> DeserializeTags(string? tags)
  {
    return JsonSerializer.Deserialize<List<string>>(tags ?? "[]", _jsonOptions) ?? [];
  }
}
