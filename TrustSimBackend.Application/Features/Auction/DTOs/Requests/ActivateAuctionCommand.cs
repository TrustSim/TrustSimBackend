using MediatR;

namespace TrustSimBackend.Application.Features.Auction.DTOs.Requests;

public record ActivateAuctionCommand(Guid AuctionId) : IRequest<bool>;
