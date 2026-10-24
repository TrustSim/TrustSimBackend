using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Ai.DTOs.Requests;
using TrustSimBackend.Application.Features.Ai.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Ai.Queries;

public class GetConsultationMessagesQueryHandler : IRequestHandler<GetConsultationMessagesQuery, PagedResult<ConsultationMessageDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetConsultationMessagesQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<ConsultationMessageDto>> Handle(GetConsultationMessagesQuery request, CancellationToken cancellationToken)
    {
        var session = await _unitOfWork.AiConsultationSessions.GetByIdAsync(request.SessionId, includeMessages: false, cancellationToken);
        if (session == null)
        {
            throw new NotFoundException($"Không tìm thấy phiên tư vấn ID {request.SessionId}");
        }

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
