using MediatR;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

public record AdminExportUsersQuery(
    string? Keyword = null,
    UserRole? Role = null,
    bool? IsActive = null
) : IRequest<byte[]>;
