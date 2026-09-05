using MediatR;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Auction.DTOs.Requests;

public record CreateAuctionCommand(
    Guid SimNumberId,
    AuctionType AuctionType,
    decimal StartingPrice,
    decimal ReservePrice,
    DateTime StartAt,
    DateTime EndAt,
    DateTime RegistrationStartAt,
    DateTime RegistrationEndAt,
    Guid AuthorId = default
) : IRequest<AuctionDto>;
