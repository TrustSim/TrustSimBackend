using MediatR;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Auction.DTOs.Requests;

public record PlaceBidCommand(
    decimal BidAmount,
    Guid AuctionId = default,
    Guid UserId = default
) : IRequest<BidDto>;
