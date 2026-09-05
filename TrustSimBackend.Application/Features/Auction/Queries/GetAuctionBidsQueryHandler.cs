using MediatR;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Queries;

public class GetAuctionBidsQueryHandler : IRequestHandler<GetAuctionBidsQuery, IReadOnlyList<BidDto>>
{
    private readonly IBidRepository _bidRepository;

    public GetAuctionBidsQueryHandler(IBidRepository bidRepository)
    {
        _bidRepository = bidRepository;
    }

    public async Task<IReadOnlyList<BidDto>> Handle(GetAuctionBidsQuery request, CancellationToken cancellationToken)
    {
        var bids = await _bidRepository.GetBidsByAuctionIdAsync(request.AuctionId, request.Top, cancellationToken);
        return bids.Select(b => new BidDto
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
    }
}
