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
    int MaxStepMultiplier = 10,
    int MaxSnipeExtensionsPerParticipant = 3,
    int PaymentGracePeriodHours = 24,
    int? PaymentGracePeriodMinutes = null,
    decimal DutchDecreaseAmount = 0m,
    float DutchDecreaseIntervalSeconds = 0f,
    decimal DepositAmount = 0m,
    int MaximumBidCountPerParticipant = 50,
    int MaximumBidCountPerMinute = 10,
    float MinimumBidIntervalSeconds = 5.0f,
    float ExtensionThresholdSeconds = 120.0f,
    float ExtensionDurationSeconds = 300.0f,
    int MaximumExtensionCount = 5,
    decimal MarginPercentage = 0.10m,
    decimal MaxPriceGapRatioForRunnerUp = 1.50m,
    int MinimumUniqueBiddersForRunnerUp = 2,
    bool RequireSellerApprovalForRunnerUp = true,
    decimal SellerDepositAmount = 0m,
    decimal SellerDepositRate = 0m,
    int SigningGracePeriodHours = 24,
    int? SigningGracePeriodMinutes = null,
    Guid AuthorId = default
) : IRequest<AuctionDto>;
