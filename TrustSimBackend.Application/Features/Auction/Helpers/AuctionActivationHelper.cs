using TrustSimBackend.Application.Features.Auction.Services;
using TrustSimBackend.Domain.Enums;
using AuctionEntity = TrustSimBackend.Domain.Entities.Auctions.Auction;

namespace TrustSimBackend.Application.Features.Auction.Helpers;

/// <summary>
/// Helper dùng chung giúp chuyển trạng thái phiên đấu giá sang ACTIVE và đóng băng nạp/rút ví người tham gia nhất quán.
/// </summary>
public static class AuctionActivationHelper
{
    /// <summary>
    /// Chuyển trạng thái phiên sang ACTIVE (nếu đang SCHEDULE) và đóng băng ví các người tham gia trong suốt phiên.
    /// </summary>
    public static bool ActivateAndFreezeWallets(AuctionEntity auction)
    {
        var changed = false;

        if (auction.AuctionStatus == AuctionStatus.SCHEDULE)
        {
            auction.AuctionStatus = AuctionStatus.ACTIVE;
            auction.LockVersion++;
            changed = true;
        }

        if (auction.Participants == null)
        {
            return changed;
        }

        foreach (var participant in auction.Participants)
        {
            var wallet = participant.UserAccount?.Wallet;
            if (wallet != null && wallet.WalletStatus == WalletStatus.ACTIVE)
            {
                wallet.WalletStatus = WalletStatus.FROZEN;
                wallet.LockVersion++;
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// Tạo job tiếp theo sau khi phiên chuyển sang ACTIVE (AUCTION_END cho English hoặc DUTCH_PRICE_DROP cho Dutch).
    /// </summary>
    public static AuctionDelayJob? BuildPostStartJob(AuctionEntity auction)
    {
        if (auction.AuctionType == AuctionType.DUTCH && auction.AuctionRule != null)
        {
            var interval = auction.AuctionRule.DutchDecreaseIntervalSeconds > 0
                ? auction.AuctionRule.DutchDecreaseIntervalSeconds
                : 60;

            return new AuctionDelayJob
            {
                AuctionId = auction.Id,
                JobType = AuctionDelayJobTypes.DUTCH_PRICE_DROP,
                ExecuteAt = auction.StartAt.AddSeconds(interval)
            };
        }

        if (auction.AuctionType == AuctionType.ENGLISH)
        {
            return new AuctionDelayJob
            {
                AuctionId = auction.Id,
                JobType = AuctionDelayJobTypes.AUCTION_END,
                ExecuteAt = auction.EndAt
            };
        }

        return null;
    }
}
