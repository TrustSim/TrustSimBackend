using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Ai.DTOs.Requests;
using TrustSimBackend.Application.Features.Ai.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Ai.Queries;

public class GetConsultationMessagesQueryHandler : IRequestHandler<GetConsultationMessagesQuery, PagedResult<ConsultationMessageDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAiAuthorizationService _aiAuthService;

    public GetConsultationMessagesQueryHandler(
        IUnitOfWork unitOfWork,
        IAiAuthorizationService aiAuthService)
    {
        _unitOfWork = unitOfWork;
        _aiAuthService = aiAuthService;
    }

    public async Task<PagedResult<ConsultationMessageDto>> Handle(GetConsultationMessagesQuery request, CancellationToken cancellationToken)
    {
        var session = await _unitOfWork.AiConsultationSessions.GetByIdAsync(request.SessionId, includeMessages: false, cancellationToken);
        if (session == null)
        {
            throw new NotFoundException($"Không tìm thấy phiên tư vấn ID {request.SessionId}");
        }

        _aiAuthService.EnsureCanAccessSession(session, request.CurrentUserId, request.CurrentUserRole);

        var (messages, totalCount) = await _unitOfWork.AiConsultationSessions.GetPagedMessagesAsync(
            request.SessionId,
            request.Filter,
            cancellationToken);

        var dtos = messages.Select(m => new ConsultationMessageDto
        {
            Id = m.Id,
            SenderRole = m.SenderRole,
            MessageContent = m.MessageContent,
            CreatedDate = m.CreatedDate
        }).ToList();

        return new PagedResult<ConsultationMessageDto>(dtos, totalCount, request.Filter.PageIndex, request.Filter.PageSize);
    }
}
