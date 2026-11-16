using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Auction.Hubs;
using TrustSimBackend.Application.Features.Auction.Services;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Infrastructure.Persistence;
using AuctionEntity = TrustSimBackend.Domain.Entities.Auctions.Auction;

namespace TrustSimBackend.Infrastructure.Workers.Auction.DelayJobs;

/// <summary>
/// Xử lý nhịp hạ giá tự động cho đấu giá kiểu Hà Lan (DUTCH_PRICE_DROP) và đóng phiên khi chạm sàn không có người mua.
/// </summary>
public sealed class DutchPriceDropJobHandler : IAuctionDelayJobHandler
{
    private readonly TrustSimDbContext _context;
    private readonly IAuctionRealtimeNotifier _realtimeNotifier;
    private readonly ILedgerService _ledgerService;
    private readonly IAuctionDelayQueueService _delayQueue;
    private readonly SellerListingDepositHelper _depositHelper;
    private readonly IRedisAuctionCacheService? _redisCache;
    private readonly INotificationService? _notificationService;

    public DutchPriceDropJobHandler(
        TrustSimDbContext context,
        IAuctionRealtimeNotifier realtimeNotifier,
        ILedgerService ledgerService,
        IAuctionDelayQueueService delayQueue,
        SellerListingDepositHelper depositHelper,
        IRedisAuctionCacheService? redisCache = null,
        INotificationService? notificationService = null)
    {
        _context = context;
        _realtimeNotifier = realtimeNotifier;
        _ledgerService = ledgerService;
        _delayQueue = delayQueue;
        _depositHelper = depositHelper;
        _redisCache = redisCache;
        _notificationService = notificationService;
    }

    public bool CanHandle(string jobType) => jobType == AuctionDelayJobTypes.DUTCH_PRICE_DROP;

    public async Task HandleAsync(AuctionDelayJob job, CancellationToken cancellationToken)
    {
        var auction = await _context.Auctions
            .Include(a => a.AuctionRule)
            .Include(a => a.SimNumber)
            .Include(a => a.Participants)
                .ThenInclude(p => p.Deposits)
            .Include(a => a.Participants)
                .ThenInclude(p => p.UserAccount)
                    .ThenInclude(u => u!.Wallet)
            .FirstOrDefaultAsync(a => a.Id == job.AuctionId, cancellationToken);

        if (auction == null || auction.AuctionStatus != AuctionStatus.ACTIVE || auction.AuctionRule == null)
        {
            return;
        }

        var rule = auction.AuctionRule;
        var interval = rule.DutchDecreaseIntervalSeconds > 0 ? rule.DutchDecreaseIntervalSeconds : 60;
        var now = DateTime.UtcNow;
        var elapsed = (now - auction.StartAt).TotalSeconds;
        var cycles = Math.Max(0, (int)(elapsed / interval));
        var calculatedPrice = auction.StartingPrice - (cycles * rule.DutchDecreaseAmount);
        var newCurrentPrice = Math.Max(calculatedPrice, auction.ReservePrice);

        auction.CurrentPrice = newCurrentPrice;
        auction.LockVersion++;

        // Kiểm tra nếu chạm giá sàn và hết thời gian mà không ai mua
        if (newCurrentPrice <= auction.ReservePrice && (now - auction.EndAt).TotalSeconds >= 0)
        {
            await CloseUnsoldDutchAuctionAsync(auction, now, cancellationToken);
            return;
        }

        await _context.SaveChangesAsync(cancellationToken);

        if (_redisCache != null)
        {
            await _redisCache.UpdateAuctionPriceInCacheAsync(auction.Id, newCurrentPrice, auction.LockVersion, cancellationToken);
        }

        var nextDropInSeconds = (int)Math.Max(0, ((cycles + 1) * interval) - elapsed);
        await _realtimeNotifier.NotifyPriceDropAsync(auction.Id, newCurrentPrice, nextDropInSeconds, cancellationToken);

        var nextDropAt = auction.StartAt.AddSeconds((cycles + 1) * interval);
        await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
        {
            AuctionId = auction.Id,
            JobType = AuctionDelayJobTypes.DUTCH_PRICE_DROP,
            ExecuteAt = nextDropAt
        }, cancellationToken);
    }

    private async Task CloseUnsoldDutchAuctionAsync(AuctionEntity auction, DateTime now, CancellationToken cancellationToken)
    {
        auction.AuctionStatus = AuctionStatus.END;
        auction.IsActive = false;

        if (auction.SimNumber != null)
        {
            auction.SimNumber.SimStatus = SimStatus.AVAILABLE;
        }

        var result = new AuctionResult
        {
            Id = Guid.NewGuid(),
            AuctionId = auction.Id,
            WinnerId = null,
            FinalPrice = auction.CurrentPrice,
            AuctionResultStatus = AuctionResultStatus.FAILED,
            CancelledAt = now,
            CancelledReason = "Phiên đấu giá Hà Lan đã chạm giá sàn hoặc hết giờ mà không có người mua. Toàn bộ tiền cọc đã được hoàn trả.",
            DeterminedAt = now,
            ConfirmedAt = now
        };
        await _context.AuctionResults.AddAsync(result, cancellationToken);

        foreach (var participant in auction.Participants)
        {
            participant.Status = ParticipantStatus.REFUNDED;
            var wallet = participant.UserAccount?.Wallet;
            var deposit = participant.Deposits.FirstOrDefault(d => d.DepositStatus == DepositStatus.LOCKED);

            if (wallet != null && deposit != null)
            {
                deposit.DepositStatus = DepositStatus.RELEASED;
                deposit.ReleasedAT = now;

                var relJournal = await _ledgerService.PostAuctionDepositReleaseAsync(
                    wallet.Id,
                    deposit.DepositAmount,
                    deposit.Id,
                    cancellationToken);

                var refundTx = new AuctionTransaction
                {
                    Id = Guid.NewGuid(),
                    WalletId = wallet.Id,
                    AuctionId = auction.Id,
                    AuctionDeposit = deposit,
                    AuctionTransactionType = AuctionTransactionType.AUCTION_DEPOSIT_RELEASED,
                    TransactionStatus = TransactionStatus.COMPLETED,
                    Amount = deposit.DepositAmount,
                    BalanceBefore = wallet.AvailableBalance,
                    BalanceAfter = wallet.AvailableBalance,
                    LedgerJournalId = relJournal.Id,
                    JournalCode = relJournal.JournalCode,
                    CreatedAt = now,
                    CompletedAt = now
                };
                await _context.AuctionTransactions.AddAsync(refundTx, cancellationToken);

                if (_notificationService != null)
                {
                    await _notificationService.SendNotificationAsync(
                        userId: participant.UserAccountId,
                        type: NotificationType.AUCTION_DEPOSIT_REFUNDED,
                        title: $"ℹ️ Hoàn tiền cọc phiên {auction.AuctionCode}",
                        content: $"Phiên đấu giá SIM {auction.SimNumber?.PhoneNumber} kết thúc không có người mua. Tiền cọc {deposit.DepositAmount:N0} VNĐ của bạn đã được hoàn trả về ví khả dụng.",
                        referenceType: "Auction",
                        referenceId: auction.Id,
                        sendEmail: true,
                        cancellationToken: cancellationToken);
                }
            }

            if (wallet != null)
            {
                wallet.WalletStatus = WalletStatus.ACTIVE;
            }
        }

        if (auction.AuctionRule?.SellerDepositAmount > 0)
        {
            var sellerWallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserAccountId == auction.AuthorId, cancellationToken);
            await _depositHelper.TryReleaseSellerListingDepositAsync(
                _context,
                _ledgerService,
                sellerWallet,
                auction.Id,
                auction.AuctionRule.SellerDepositAmount,
                now,
                cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        if (_redisCache != null)
        {
            await _redisCache.EvictAuctionCacheWithTtlAsync(auction.Id, TimeSpan.FromHours(24), cancellationToken);
        }

        await _realtimeNotifier.NotifyAuctionStatusChangedAsync(
            auction.Id,
            AuctionStatus.END.ToString(),
            null,
            auction.CurrentPrice,
            cancellationToken);

        await _realtimeNotifier.NotifyAuctionFailedAsync(
            auction.Id,
            "Phiên đấu giá Hà Lan đã chạm giá sàn mà không có người mua.",
            cancellationToken);

        if (_notificationService != null)
        {
            await _notificationService.SendNotificationAsync(
                userId: auction.AuthorId,
                type: NotificationType.AUCTION_FAILED,
                title: $"ℹ️ Phiên đấu giá SIM {auction.SimNumber?.PhoneNumber} kết thúc thất bại",
                content: $"Phiên đấu giá Hà Lan {auction.AuctionCode} đã kết thúc mà không có người mua. SIM đã được chuyển về trạng thái Sẵn sàng.",
                referenceType: "Auction",
                referenceId: auction.Id,
                sendEmail: true,
                cancellationToken: cancellationToken);
        }
    }
}
