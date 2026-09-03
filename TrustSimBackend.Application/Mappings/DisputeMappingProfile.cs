using AutoMapper;
using TrustSimBackend.Domain.Entities.Dispute;

namespace TrustSimBackend.Application.Mappings;

/// <summary>
/// Cấu hình AutoMapper cho phân hệ Khiếu nại & Tranh chấp (Dispute).
/// </summary>
public class DisputeMappingProfile : Profile
{
    public DisputeMappingProfile()
    {
        CreateMap<DisputeTicket, object>();
        CreateMap<DisputeProof, object>();
    }
}
