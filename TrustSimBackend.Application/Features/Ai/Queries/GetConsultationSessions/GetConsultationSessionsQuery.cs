using MediatR;
using TrustSimBackend.Application.Features.Ai.DTOs;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Ai.Queries.GetConsultationSessions;

public record GetConsultationSessionsQuery(Guid UserId) : IRequest<IReadOnlyList<ConsultationSessionDto>>;

public class GetConsultationSessionsQueryHandler : IRequestHandler<GetConsultationSessionsQuery, IReadOnlyList<ConsultationSessionDto>>
{
    private readonly IAiConsultationSessionRepository _sessionRepository;

    public GetConsultationSessionsQueryHandler(IAiConsultationSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository;
    }

    public async Task<IReadOnlyList<ConsultationSessionDto>> Handle(GetConsultationSessionsQuery request, CancellationToken cancellationToken)
    {
        var sessions = await _sessionRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        return sessions.Select(s => new ConsultationSessionDto
        {
            Id = s.Id,
            SessionTitle = s.SessionTitle,
            TargetBudgetMin = s.TargetBudgetMin,
            TargetBudgetMax = s.TargetBudgetMax,
            UserBirthYear = s.UserBirthYear,
            UserCareer = s.UserCareer,
            FengShuiElement = s.FengShuiElement,
            CreatedDate = s.CreatedDate,
            MessageCount = s.Messages.Count
        }).ToList();
    }
}
