using MediatR;

namespace TrustSimBackend.Application.Features.Auction.DTOs.Requests;

public record RegisterParticipantCommand(
    Guid AuctionId = default,
    Guid UserId = default
) : IRequest<bool>;
