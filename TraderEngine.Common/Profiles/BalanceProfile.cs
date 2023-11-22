using AutoMapper;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Models;

namespace TraderEngine.Common.Profiles;

internal class BalanceProfile : Profile
{
  public BalanceProfile()
  {
    CreateMap<Balance, BalanceDto>()
      .ForMember(
        dest => dest.Allocations, opt => opt.MapFrom(
          src => src.Allocations
          .OrderBy(alloc => !alloc.Market.BaseSymbol.Equals(src.QuoteSymbol))
          .ThenByDescending(alloc => alloc.AmountQuote)));
  }
}