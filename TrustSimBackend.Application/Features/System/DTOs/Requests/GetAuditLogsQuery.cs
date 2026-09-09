using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.System.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.System.DTOs.Requests;

public record GetAuditLogsQuery(
    string? EntityName = null,
    Guid? EntityId = null,
    Guid? ActorId = null,
    ActionType? ActionType = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int PageIndex = 1,
    int PageSize = 20
) : IRequest<PagedResult<AuditLogDto>>;
