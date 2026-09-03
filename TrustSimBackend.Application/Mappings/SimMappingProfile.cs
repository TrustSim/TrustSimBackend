using AutoMapper;
using TrustSimBackend.Domain.Entities.Sim;

namespace TrustSimBackend.Application.Mappings;

/// <summary>
/// Cấu hình AutoMapper cho phân hệ Quản lý SIM số đẹp.
/// </summary>
public class SimMappingProfile : Profile
{
    public SimMappingProfile()
    {
        CreateMap<SimNumber, object>();
        CreateMap<SimCarrier, object>();
        CreateMap<SimCategory, object>();
        CreateMap<PhonePrefix, object>();
    }
}
