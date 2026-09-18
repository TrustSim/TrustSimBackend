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
    decimal MinimumBidIncrement = 500_000m,
    decimal DutchDecreaseAmount = 0m,
    float DutchDecreaseIntervalSeconds = 0f,
    decimal DepositAmount = 0m,
    int MaximumBidCountPerParticipant = 50,
    int MaximumBidCountPerMinute = 10,
    float MinimumBidIntervalSeconds = 5.0f,
    float ExtensionThresholdSeconds = 120.0f,
    float ExtensionDurationSeconds = 300.0f,
    int MaximumExtensionCount = 5,
    Guid AuthorId = default
) : IRequest<AuctionDto>;
