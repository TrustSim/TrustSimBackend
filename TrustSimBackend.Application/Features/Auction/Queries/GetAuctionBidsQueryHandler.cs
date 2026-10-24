using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Queries;

public class GetAuctionBidsQueryHandler : IRequestHandler<GetAuctionBidsQuery, PagedResult<BidDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetAuctionBidsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<BidDto>> Handle(GetAuctionBidsQuery request, CancellationToken cancellationToken)
    {
        var (bids, totalCount) = await _unitOfWork.Bids.GetPagedBidsByAuctionIdAsync(
            request.AuctionId,
            request.Filter,
            cancellationToken);

        var dtos = bids.Select(b => new BidDto
        {
            Id = b.Id,
            AuctionId = b.AuctionId,
            ParticipantId = b.ParticipantId,
            UserAccountId = b.Participant?.UserAccountId ?? Guid.Empty,
            UserNameMasked = b.Participant?.UserAccount?.UserName != null && b.Participant.UserAccount.UserName.Length > 3 
                ? b.Participant.UserAccount.UserName[..2] + "***" + b.Participant.UserAccount.UserName[^1..] 
                : "***",
            BidAmount = b.BidAmount,
            PlacedAt = b.PlacedAt,
            Status = b.Status
        }).ToList();

        return new PagedResult<BidDto>(dtos, totalCount, request.Filter.PageIndex, request.Filter.PageSize);
    }
}
