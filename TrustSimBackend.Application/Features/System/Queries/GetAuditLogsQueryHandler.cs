using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.System.DTOs.Requests;
using TrustSimBackend.Application.Features.System.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.System.Queries;

public class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, PagedResult<AuditLogDto>>
{
    private readonly IAuditLogRepository _repository;

    public GetAuditLogsQueryHandler(IAuditLogRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<AuditLogDto>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _repository.GetPagedAsync(
            request.EntityName,
            request.EntityId,
            request.ActorId,
            request.ActionType,
            request.FromDate,
            request.ToDate,
            request.PageIndex,
            request.PageSize,
            cancellationToken);

        var dtos = items.Select(log => new AuditLogDto
        {
            Id = log.Id,
            AuditLogName = log.AuditLogName,
            ActionType = log.ActionType,
            EntityName = log.EntityName,
            EntityId = log.EntityId,
            ActorId = log.ActorId,
            ActorName = log.UserAccount?.UserProfile?.FullName ?? log.UserAccount?.UserName ?? string.Empty,
            IPAddress = log.IPAddress,
            OldData = log.OldData,
            NewData = log.NewData,
            CreatedDate = log.CreatedDate
        }).ToList();

        return new PagedResult<AuditLogDto>(dtos, totalCount, request.PageIndex, request.PageSize);
    }
}
