using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Auction.DTOs;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Queries.GetAuctionDetail;

/// <summary>
/// Query lấy chi tiết 1 phiên đấu giá theo ID hoặc AuctionCode.
/// </summary>
public record GetAuctionDetailQuery(
    Guid? AuctionId = null,
    string? AuctionCode = null
) : IRequest<ApiResponse<AuctionDetailDto>>;

public class GetAuctionDetailQueryHandler : IRequestHandler<GetAuctionDetailQuery, ApiResponse<AuctionDetailDto>>
{
    private readonly IAuctionRepository _auctionRepository;

    public GetAuctionDetailQueryHandler(IAuctionRepository auctionRepository)
    {
        _auctionRepository = auctionRepository;
    }

    public async Task<ApiResponse<AuctionDetailDto>> Handle(GetAuctionDetailQuery request, CancellationToken cancellationToken)
    {
        Domain.Entities.Auctions.Auction? auction = null;

        if (request.AuctionId.HasValue)
        {
            auction = await _auctionRepository.GetDetailByIdAsync(request.AuctionId.Value, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(request.AuctionCode))
        {
            var simple = await _auctionRepository.GetByCodeAsync(request.AuctionCode.Trim(), cancellationToken);
            if (simple != null)
                auction = await _auctionRepository.GetDetailByIdAsync(simple.Id, cancellationToken);
        }

        if (auction == null)
            throw new NotFoundException("Không tìm thấy phiên đấu giá.");

        var dto = new AuctionDetailDto
        {
            AuctionId = auction.Id,
            AuctionCode = auction.AuctionCode,
            AuctionType = auction.AuctionType,
            AuctionStatus = auction.AuctionStatus,
            SimNumberId = auction.SimNumberId,
            PhoneNumber = auction.SimNumber?.PhoneNumber ?? string.Empty,
            CarrierName = auction.SimNumber?.PhonePrefix?.SimCarrier?.CarrierName,
            FormatRhythmName = auction.SimNumber?.FormatRhythm?.FormatRhythmName,
            StartingPrice = auction.StartingPrice,
            CurrentPrice = auction.CurrentPrice,
            ReservePrice = auction.ReservePrice,
            StartAt = auction.StartAt,
            EndAt = auction.EndAt,
            RegistrationStartAt = auction.RegistrationStartAt,
            RegistrationEndAt = auction.RegistrationEndAt,
            AuthorId = auction.AuthorId,
            AuthorName = auction.Author?.UserProfile?.FullName ?? auction.Author?.UserName,
            AuthorEmail = auction.Author?.UserEmail,
            Categories = auction.SimNumber?.SimCategoryMappings
                .Select(m => m.SimCategory?.CategoryName ?? string.Empty).ToList() ?? new(),
            Rule = auction.AuctionRule == null ? null : new AuctionRuleDto
            {
                MinimumBidIncrement = auction.AuctionRule.MinimumBidIncrement,
                DepositAmount = auction.AuctionRule.DepositAmount,
                MaximumBidCountPerParticipant = auction.AuctionRule.MaximumBidCountPerParticipant,
                MaximumBidCountPerMinute = auction.AuctionRule.MaximumBidCountPerMinute,
                MinimumBidIntervalSeconds = auction.AuctionRule.MinimumBidIntervalSeconds,
                ExtensionThresholdSeconds = auction.AuctionRule.ExtensionThresholdSeconds,
                ExtensionDurationSeconds = auction.AuctionRule.ExtensionDurationSeconds,
                MaximumExtensionCount = auction.AuctionRule.MaximumExtensionCount,
                DutchDecreaseAmount = auction.AuctionRule.DutchDecreaseAmount,
                DutchDecreaseIntervalSeconds = auction.AuctionRule.DutchDecreaseIntervalSeconds,
            },
            CreatedDate = auction.CreatedDate,
            UpdatedDate = auction.UpdatedDate
        };

        return ApiResponse<AuctionDetailDto>.SuccessResult(dto);
    }
}
