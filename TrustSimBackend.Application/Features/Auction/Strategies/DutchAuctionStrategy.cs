using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Auction.Commands.CreateAuction;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Auction.Strategies;

/// <summary>
/// Strategy cho đấu giá kiểu Dutch (giá giảm dần).
/// Quy tắc: Giá tự động giảm DutchDecreaseAmount sau mỗi DutchDecreaseIntervalSeconds giây,
/// người đầu tiên "chấp nhận giá" (Accept) thắng ngay lập tức.
/// </summary>
public class DutchAuctionStrategy : IAuctionStrategy
{
    public AuctionType AuctionType => AuctionType.DUTCH;

    public void ValidateRule(CreateAuctionCommand request)
    {
        if (request.DutchDecreaseAmount <= 0)
            throw new BadRequestException(
                "Đấu giá kiểu Dutch (giá giảm dần) yêu cầu mức giảm giá mỗi chu kỳ (DutchDecreaseAmount) phải lớn hơn 0.");

        if (request.DutchDecreaseIntervalSeconds <= 0)
            throw new BadRequestException(
                "Đấu giá kiểu Dutch (giá giảm dần) yêu cầu khoảng thời gian giảm giá (DutchDecreaseIntervalSeconds) phải lớn hơn 0 giây.");

        if (request.ReservePrice <= 0)
            throw new BadRequestException(
                "Đấu giá kiểu Dutch yêu cầu giá sàn (ReservePrice) lớn hơn 0 để xác định điểm dừng giảm giá.");
    }

    public AuctionRule BuildRule(Guid auctionId, CreateAuctionCommand request)
    {
        return new AuctionRule
        {
            Id = Guid.NewGuid(),
            AuctionId = auctionId,
            // English-specific fields: không áp dụng
            MinimumBidIncrement = 0,
            // Dutch-specific
            DutchDecreaseAmount = request.DutchDecreaseAmount,
            DutchDecreaseIntervalSeconds = request.DutchDecreaseIntervalSeconds,
            // Common fields
            DepositAmount = request.DepositAmount,
            MaximumBidCountPerParticipant = request.MaximumBidCountPerParticipant,
            MaximumBidCountPerMinute = request.MaximumBidCountPerMinute,
            MinimumBidIntervalSeconds = request.MinimumBidIntervalSeconds,
            // Dutch không cần Anti-Sniping vì kết thúc khi người đầu tiên chấp nhận
            ExtensionThresholdSeconds = 0,
            ExtensionDurationSeconds = 0,
            MaximumExtensionCount = 0,
            IsActive = true
        };
    }

    public decimal GetInitialCurrentPrice(CreateAuctionCommand request)
        => request.StartingPrice; // Bắt đầu từ giá cao nhất, giảm dần về ReservePrice
}
