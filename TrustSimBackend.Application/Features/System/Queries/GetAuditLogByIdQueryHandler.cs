using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.System.DTOs.Requests;
using TrustSimBackend.Application.Features.System.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.System.Queries;

public class GetAuditLogByIdQueryHandler : IRequestHandler<GetAuditLogByIdQuery, AuditLogDto>
{
    private readonly IAuditLogRepository _repository;

    public GetAuditLogByIdQueryHandler(IAuditLogRepository repository)
    {
        _repository = repository;
    }

    public async Task<AuditLogDto> Handle(GetAuditLogByIdQuery request, CancellationToken cancellationToken)
    {
        var log = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (log == null) throw new NotFoundException($"Không tìm thấy nhật ký kiểm toán với ID '{request.Id}'.");

        return new AuditLogDto
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
        };
    }
}
