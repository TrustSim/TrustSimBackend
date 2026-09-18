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
            MaximumExtensionCount = request.MaximumExtensionCount,
            IsActive = true
        };
    }

    public decimal GetInitialCurrentPrice(CreateAuctionCommand request)
        => request.StartingPrice; // Giá khởi điểm là giá thấp nhất, tăng dần
}
