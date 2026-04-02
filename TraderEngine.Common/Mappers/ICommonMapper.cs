using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.DTOs.Database;
using TraderEngine.Common.Models;

namespace TraderEngine.Common.Mappers;

public interface ICommonMapper
{
  public AllocationDto MapAllocation(Allocation source);

  public BalanceDto MapBalance(Balance source);

  public MarketCapDataDb MarketCapToDb(MarketCapDataDto source);

  public MarketCapDataDto MarketCapFromDb(MarketCapDataDb source);

  public IEnumerable<MarketCapDataDto> MarketCapsFromDb(IEnumerable<MarketCapDataDb> source);
}
