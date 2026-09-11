using MediatR;

namespace TrustSimBackend.Application.Features.Auction.DTOs.Requests;

public record AdminCancelAuctionCommand(Guid AuctionId, string Reason, Guid AdminId) : IRequest<bool>;
