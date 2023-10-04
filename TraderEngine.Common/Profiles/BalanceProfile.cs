using AutoMapper;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Models;

namespace TraderEngine.Common.Profiles;

internal class BalanceProfile : Profile
{
  public BalanceProfile()
  {
    CreateMap<Balance, BalanceDto>();
  }
}