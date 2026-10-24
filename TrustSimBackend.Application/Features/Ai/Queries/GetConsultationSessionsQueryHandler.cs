using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Ai.DTOs.Requests;
using TrustSimBackend.Application.Features.Ai.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Ai.Queries;

public class GetConsultationSessionsQueryHandler : IRequestHandler<GetConsultationSessionsQuery, PagedResult<ConsultationSessionDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetConsultationSessionsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<ConsultationSessionDto>> Handle(GetConsultationSessionsQuery request, CancellationToken cancellationToken)
    {
        var (sessions, totalCount) = await _unitOfWork.AiConsultationSessions.GetPagedSessionsByUserIdAsync(
            request.UserId,
            request.Filter,
            cancellationToken);

        var dtos = sessions.Select(s => new ConsultationSessionDto
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

        return new PagedResult<ConsultationSessionDto>(dtos, totalCount, request.Filter.PageIndex, request.Filter.PageSize);
    }
}
