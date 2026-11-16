using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Auction.Helpers;
using TrustSimBackend.Application.Features.Auction.Hubs;
using TrustSimBackend.Application.Features.Auction.Services;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Infrastructure.Persistence;
using AuctionEntity = TrustSimBackend.Domain.Entities.Auctions.Auction;

namespace TrustSimBackend.Infrastructure.Workers.Auction.DelayJobs;

/// <summary>
/// Xử lý pha Pre-Warm (StartAt - 5s) và kích hoạt mở phiên (AUCTION_START) với độ trễ tối thiểu.
/// </summary>
public sealed class AuctionPreWarmAndStartJobHandler : IAuctionDelayJobHandler
{
    private readonly TrustSimDbContext _context;
    private readonly IAuctionRealtimeNotifier _realtimeNotifier;
    private readonly ILedgerService _ledgerService;
    private readonly IAuctionDelayQueueService _delayQueue;
    private readonly SellerListingDepositHelper _depositHelper;
    private readonly ILogger<AuctionPreWarmAndStartJobHandler> _logger;
    private readonly IRedisAuctionCacheService? _redisCache;
    private readonly INotificationService? _notificationService;

    public AuctionPreWarmAndStartJobHandler(
        TrustSimDbContext context,
        IAuctionRealtimeNotifier realtimeNotifier,
        ILedgerService ledgerService,
        IAuctionDelayQueueService delayQueue,
        SellerListingDepositHelper depositHelper,
        ILogger<AuctionPreWarmAndStartJobHandler> logger,
        IRedisAuctionCacheService? redisCache = null,
        INotificationService? notificationService = null)
    {
        _context = context;
        _realtimeNotifier = realtimeNotifier;
        _ledgerService = ledgerService;
        _delayQueue = delayQueue;
        _depositHelper = depositHelper;
        _logger = logger;
        _redisCache = redisCache;
        _notificationService = notificationService;
    }

    public bool CanHandle(string jobType)
        => jobType == AuctionDelayJobTypes.AUCTION_PRE_WARM || jobType == AuctionDelayJobTypes.AUCTION_START;

    public Task HandleAsync(AuctionDelayJob job, CancellationToken cancellationToken)
        => job.JobType == AuctionDelayJobTypes.AUCTION_PRE_WARM
            ? HandlePreWarmAsync(job.AuctionId, cancellationToken)
            : HandleAuctionStartAsync(job.AuctionId, cancellationToken);

    private async Task HandlePreWarmAsync(Guid auctionId, CancellationToken cancellationToken)
    {
        if (_redisCache == null) return;

        var auction = await _context.Auctions
            .AsNoTracking()
            .Include(a => a.AuctionRule)
            .Include(a => a.Participants)
                .ThenInclude(p => p.Deposits)
            .Include(a => a.Participants)
                .ThenInclude(p => p.UserAccount)
                    .ThenInclude(u => u!.Wallet)
            .Include(a => a.Bids)
            .FirstOrDefaultAsync(a => a.Id == auctionId, cancellationToken);

        if (auction == null || auction.AuctionStatus != AuctionStatus.SCHEDULE)
        {
            return;
        }

        await _redisCache.WarmUpAuctionAsync(auction, cancellationToken);
    }

    private async Task HandleAuctionStartAsync(Guid auctionId, CancellationToken cancellationToken)
    {
        // Bật nhanh trạng thái SCHEDULE -> ACTIVE trên Redis RAM (<1ms) nếu đã được Pre-Warm trước 5 giây
        var activatedInCache = _redisCache != null && await _redisCache.ActivateAuctionInCacheAsync(auctionId, cancellationToken);

        var auction = await _context.Auctions
            .Include(a => a.AuctionRule)
            .Include(a => a.SimNumber)
            .Include(a => a.Participants)
                .ThenInclude(p => p.Deposits)
            .Include(a => a.Participants)
                .ThenInclude(p => p.UserAccount)
                    .ThenInclude(u => u!.Wallet)
            .FirstOrDefaultAsync(a => a.Id == auctionId, cancellationToken);

        if (auction == null || auction.AuctionStatus != AuctionStatus.SCHEDULE)
        {
            return;
        }

        var now = DateTime.UtcNow;

        // KIỂM TRA 1: PHIÊN BẮT ĐẦU MÀ KHÔNG CÓ AI ĐĂNG KÝ THAM GIA -> HỦY THẤT BẠI
        if (auction.Participants.Count == 0)
        {
            await CancelAuctionWithoutParticipantsAsync(auction, now, cancellationToken);
            return;
        }

        AuctionActivationHelper.ActivateAndFreezeWallets(auction);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Trường hợp luồng JIT đặt giá vừa kịp chuyển sang ACTIVE trước vài mili-giây
            var reloaded = await _context.Auctions.AsNoTracking().FirstOrDefaultAsync(a => a.Id == auctionId, cancellationToken);
            if (reloaded?.AuctionStatus == AuctionStatus.ACTIVE)
            {
                return;
            }

            throw;
        }

        if (_redisCache != null && !activatedInCache)
        {
            await _redisCache.WarmUpAuctionAsync(auction, cancellationToken);
        }

        await _realtimeNotifier.NotifyAuctionStatusChangedAsync(
            auction.Id,
            AuctionStatus.ACTIVE.ToString(),
            null,
            auction.CurrentPrice,
            cancellationToken);

        _logger.LogInformation("[LiveAuctionWorker] 🟢 Phiên '{Code}' (ID: {Id}) đã mở phòng LIVE (ACTIVE).",
            auction.AuctionCode, auction.Id);

        var nextJob = AuctionActivationHelper.BuildPostStartJob(auction);
        if (nextJob != null)
        {
            await _delayQueue.EnqueueJobAsync(nextJob, cancellationToken);
        }
    }

    private async Task CancelAuctionWithoutParticipantsAsync(
        AuctionEntity auction,
        DateTime now,
        CancellationToken cancellationToken)
    {
        auction.AuctionStatus = AuctionStatus.END;
        auction.IsActive = false;
        auction.LockVersion++;

        if (auction.SimNumber != null)
        {
            auction.SimNumber.SimStatus = SimStatus.AVAILABLE;
        }

        var failedResult = new AuctionResult
        {
            Id = Guid.NewGuid(),
            AuctionId = auction.Id,
            WinnerId = null,
            FinalPrice = auction.StartingPrice,
            AuctionResultStatus = AuctionResultStatus.FAILED,
            CancelledAt = now,
            CancelledReason = "Phiên đấu giá đã đến giờ bắt đầu nhưng không có người đăng ký tham gia đặt cọc. Phiên tự động hủy và kết thúc thất bại.",
            DeterminedAt = now,
            ConfirmedAt = now
        };
        await _context.AuctionResults.AddAsync(failedResult, cancellationToken);

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
            await _redisCache.InvalidateAuctionCacheAsync(auction.Id, cancellationToken);
        }

        await _realtimeNotifier.NotifyAuctionStatusChangedAsync(
            auction.Id,
            AuctionStatus.END.ToString(),
            null,
            auction.StartingPrice,
            cancellationToken);

        await _realtimeNotifier.NotifyAuctionFailedAsync(
            auction.Id,
            "Phiên đấu giá bắt đầu nhưng không có người đăng ký tham gia đặt cọc.",
            cancellationToken);

        if (_notificationService != null)
        {
            await _notificationService.SendNotificationAsync(
                userId: auction.AuthorId,
                type: NotificationType.AUCTION_FAILED,
                title: $"ℹ️ Phiên đấu giá SIM {auction.SimNumber?.PhoneNumber} bị hủy",
                content: $"Phiên đấu giá {auction.AuctionCode} đã đến giờ bắt đầu nhưng không có người đăng ký tham gia. Phiên đã tự động hủy và SIM được chuyển về trạng thái Sẵn sàng.",
                referenceType: "Auction",
                referenceId: auction.Id,
                sendEmail: true,
                cancellationToken: cancellationToken);
        }

        _logger.LogInformation("[LiveAuctionWorker] ⛔ Phiên '{Code}' (ID: {Id}) bị hủy do không có người tham gia.",
            auction.AuctionCode, auction.Id);
    }
}
