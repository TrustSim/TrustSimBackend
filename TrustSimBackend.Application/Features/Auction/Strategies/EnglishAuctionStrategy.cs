using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Auction.Strategies;

/// <summary>
/// Strategy cho đấu giá kiểu English (giá tăng dần).
/// Quy tắc: Người tham gia trả giá cao hơn giá hiện tại ít nhất MinimumBidIncrement.
/// Hỗ trợ Anti-Sniping: gia hạn thêm giờ khi có bid sát thời điểm kết thúc.
/// </summary>
public class EnglishAuctionStrategy : IAuctionStrategy
{
    public AuctionType AuctionType => AuctionType.ENGLISH;

    public void ValidateRule(CreateAuctionCommand request)
    {
        if (request.MinimumBidIncrement <= 0)
            throw new BadRequestException(
                "Đấu giá kiểu English (giá tăng dần) yêu cầu mức tăng giá tối thiểu (MinimumBidIncrement) phải lớn hơn 0.");
    }

    public AuctionRule BuildRule(Guid auctionId, CreateAuctionCommand request)
    {
        return new AuctionRule
        {
            Id = Guid.NewGuid(),
            AuctionId = auctionId,
            // English-specific
            MinimumBidIncrement = request.MinimumBidIncrement,
            MaxStepMultiplier = request.MaxStepMultiplier > 0 ? request.MaxStepMultiplier : 10,
            PaymentGracePeriodHours = request.PaymentGracePeriodHours > 0 ? request.PaymentGracePeriodHours : 24,
            PaymentGracePeriodMinutes = request.PaymentGracePeriodMinutes.HasValue && request.PaymentGracePeriodMinutes.Value > 0
                ? request.PaymentGracePeriodMinutes.Value
                : (request.PaymentGracePeriodHours > 0 ? request.PaymentGracePeriodHours * 60 : 1440),
            // Dutch-specific fields: không áp dụng
            DutchDecreaseAmount = 0,
            DutchDecreaseIntervalSeconds = 0,
            // Common fields
            DepositAmount = request.DepositAmount,
            MaximumBidCountPerParticipant = request.MaximumBidCountPerParticipant,
            MaximumBidCountPerMinute = request.MaximumBidCountPerMinute,
            MinimumBidIntervalSeconds = request.MinimumBidIntervalSeconds,
            ExtensionThresholdSeconds = request.ExtensionThresholdSeconds,
            ExtensionDurationSeconds = request.ExtensionDurationSeconds,
            // Anti-Shill & Bid Shielding
            MarginPercentage = request.MarginPercentage > 0 ? request.MarginPercentage : 0.10m,
            MaxPriceGapRatioForRunnerUp = request.MaxPriceGapRatioForRunnerUp > 0 ? request.MaxPriceGapRatioForRunnerUp : 1.50m,
            MinimumUniqueBiddersForRunnerUp = request.MinimumUniqueBiddersForRunnerUp > 0 ? request.MinimumUniqueBiddersForRunnerUp : 2,
            RequireSellerApprovalForRunnerUp = request.RequireSellerApprovalForRunnerUp,
            IsActive = true
        };
    }

    public decimal GetInitialCurrentPrice(CreateAuctionCommand request)
        => request.StartingPrice; // Giá khởi điểm là giá thấp nhất, tăng dần
}
