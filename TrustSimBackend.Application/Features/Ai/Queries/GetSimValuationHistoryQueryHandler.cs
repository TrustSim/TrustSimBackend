using MediatR;
using TrustSimBackend.Application.Features.Ai.DTOs.Requests;
using TrustSimBackend.Application.Features.Ai.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Ai.Queries;

public class GetSimValuationHistoryQueryHandler : IRequestHandler<GetSimValuationHistoryQuery, IReadOnlyList<SimValuationResponseDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetSimValuationHistoryQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<SimValuationResponseDto>> Handle(GetSimValuationHistoryQuery request, CancellationToken cancellationToken)
    {
        var history = await _unitOfWork.SimValuations.GetHistoryBySimNumberIdAsync(request.SimNumberId, cancellationToken);

        return history.Select(v => new SimValuationResponseDto
        {
            ValuationId = v.Id,
            SimNumberId = v.SimNumberId,
            PhoneNumber = v.SimNumber?.PhoneNumber ?? string.Empty,
            SuggestedPrice = v.SuggestedPrice,
            MinPrice = v.MinPrice,
            MaxPrice = v.MaxPrice,
            ConfidenceScore = v.ConfidenceScore,
            BaselinePrice = v.BaselinePrice,
            MarketMultiplier = v.MarketMultiplier,
            ValuationReasoning = v.ValuationReasoning,
            FengShuiAnalysis = v.FengShuiAnalysis,
            AiModelName = v.AiModelName,
            AppliedToSim = v.AppliedToSim,
            CreatedDate = v.CreatedDate
        }).ToList();
    }
}
