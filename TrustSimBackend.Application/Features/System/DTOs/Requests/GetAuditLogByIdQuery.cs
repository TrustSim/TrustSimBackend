using MediatR;
using TrustSimBackend.Application.Features.System.DTOs.Responses;

namespace TrustSimBackend.Application.Features.System.DTOs.Requests;

public record GetAuditLogByIdQuery(Guid Id) : IRequest<AuditLogDto>;
