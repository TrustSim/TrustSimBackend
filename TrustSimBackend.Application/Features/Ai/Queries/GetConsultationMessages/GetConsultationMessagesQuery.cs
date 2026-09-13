using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Ai.DTOs;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Ai.Queries.GetConsultationMessages;

public record GetConsultationMessagesQuery(Guid SessionId) : IRequest<IReadOnlyList<ConsultationMessageDto>>;

public class GetConsultationMessagesQueryHandler : IRequestHandler<GetConsultationMessagesQuery, IReadOnlyList<ConsultationMessageDto>>
{
    private readonly IAiConsultationSessionRepository _sessionRepository;

    public GetConsultationMessagesQueryHandler(IAiConsultationSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository;
    }

    public async Task<IReadOnlyList<ConsultationMessageDto>> Handle(GetConsultationMessagesQuery request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, includeMessages: true, cancellationToken);
        if (session == null)
        {
            throw new NotFoundException($"Không tìm thấy phiên tư vấn ID {request.SessionId}");
        }

        return session.Messages.Select(m => new ConsultationMessageDto
        {
            Id = m.Id,
            SenderRole = m.SenderRole,
            MessageContent = m.MessageContent,
            CreatedDate = m.CreatedDate
        }).ToList();
    }
}
