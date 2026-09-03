using AutoMapper;
using TrustSimBackend.Domain.Entities.System;

namespace TrustSimBackend.Application.Mappings;

/// <summary>
/// Cấu hình AutoMapper cho phân hệ Hệ thống (Notification, AuditLog, Document).
/// </summary>
public class SystemMappingProfile : Profile
{
    public SystemMappingProfile()
    {
        CreateMap<Notification, object>();
        CreateMap<AuditLog, object>();
        CreateMap<Document, object>();
    }
}
