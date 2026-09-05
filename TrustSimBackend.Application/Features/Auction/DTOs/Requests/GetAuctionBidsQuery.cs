using MediatR;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Auction.DTOs.Requests;

public record GetAuctionBidsQuery(Guid AuctionId = default, int Top = 50) : IRequest<IReadOnlyList<BidDto>>;
