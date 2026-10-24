using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Ai.DTOs.Requests;
using TrustSimBackend.Application.Features.Ai.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Ai.Queries;

public class GetSimValuationHistoryQueryHandler : IRequestHandler<GetSimValuationHistoryQuery, PagedResult<SimValuationResponseDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetSimValuationHistoryQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<SimValuationResponseDto>> Handle(GetSimValuationHistoryQuery request, CancellationToken cancellationToken)
    {
        var (history, totalCount) = await _unitOfWork.SimValuations.GetPagedHistoryBySimNumberIdAsync(
            request.SimNumberId,
            request.Filter,
            cancellationToken);

        var dtos = history.Select(v => new SimValuationResponseDto
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

        return new PagedResult<SimValuationResponseDto>(dtos, totalCount, request.Filter.PageIndex, request.Filter.PageSize);
    }
}
