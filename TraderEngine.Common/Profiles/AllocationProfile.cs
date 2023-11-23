using AutoMapper;
using TraderEngine.Common.DTOs.API.Response;
using TraderEngine.Common.Models;

namespace TraderEngine.Common.Profiles;

internal class AllocationProfile : Profile
{
  public AllocationProfile()
  {
    CreateMap<Allocation, AllocationDto>();
  }
}