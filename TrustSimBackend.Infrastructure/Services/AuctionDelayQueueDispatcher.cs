using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Helpers;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;
using TrustSimBackend.Application.Features.Auction.Hubs;
using TrustSimBackend.Application.Features.Auction.Services;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;
using StackExchange.Redis;
using TrustSimBackend.Infrastructure.Persistence;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Background Service điều phối In-Memory High-Precision Timer tiêu thụ Job từ Redis ZSET Delay Queue.
/// </summary>
public class AuctionDelayQueueDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAuctionDelayQueueService _delayQueue;
    private readonly ILogger<AuctionDelayQueueDispatcher> _logger;

    public AuctionDelayQueueDispatcher(
        IServiceScopeFactory scopeFactory,
        IAuctionDelayQueueService delayQueue,
        ILogger<AuctionDelayQueueDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _delayQueue = delayQueue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[AuctionDelayDispatcher] Khởi động bộ điều phối hàng đợi trễ Redis ZSET...");

        // 1. Startup Sync: Quét nạp các phiên từ CSDL vào Delay Queue nếu chưa được nạp
        try
        {
            await SyncDatabaseAuctionsToDelayQueueAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AuctionDelayDispatcher] Lỗi trong quá trình Startup Sync.");
        }

        // 2. High-Precision Dispatch Loop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                var job = await _delayQueue.PopNextDueJobAsync(now, stoppingToken);

                if (job != null)
                {
                    _logger.LogInformation("[AuctionDelayDispatcher] ⚡ Xử lý Job {JobType} cho phiên {AuctionId} đúng hạn lúc {Time:HH:mm:ss.fff}",
                        job.JobType, job.AuctionId, now);

                    await ProcessJobAsync(job, stoppingToken);
                    // Lập tức kiểm tra tiếp xem còn job nào đến hạn cùng thời điểm không
                    continue;
                }

                // Nếu không có job đến hạn, tính thời gian ngủ đến job tiếp theo
                var nextJobTime = await _delayQueue.GetNextJobTimeAsync(stoppingToken);
                if (nextJobTime.HasValue)
                {
                    var delay = nextJobTime.Value - DateTime.UtcNow;
                    if (delay > TimeSpan.Zero)
                    {
                        var waitMs = Math.Min((int)delay.TotalMilliseconds, 1000);
                        await Task.Delay(Math.Max(waitMs, 50), stoppingToken);
                        continue;
                    }
                }

                // Mặc định ngủ 500ms khi hàng đợi trống
                await Task.Delay(500, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "[AuctionDelayDispatcher] Lỗi trong vòng lặp Dispatcher.");
                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.LogInformation("[AuctionDelayDispatcher] Dừng bộ điều phối hàng đợi trễ.");
    }

    private async Task SyncDatabaseAuctionsToDelayQueueAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrustSimDbContext>();

        var now = DateTime.UtcNow;

        // Phiên SCHEDULE
        var scheduledAuctions = await context.Auctions
            .Include(a => a.AuctionRule)
            .Where(a => a.AuctionStatus == AuctionStatus.SCHEDULE && a.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var auc in scheduledAuctions)
        {
            await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
            {
                AuctionId = auc.Id,
                JobType = AuctionDelayJobTypes.AUCTION_START,
                ExecuteAt = auc.StartAt
            }, cancellationToken);
        }

        // Phiên ACTIVE
        var activeAuctions = await context.Auctions
            .Include(a => a.AuctionRule)
            .Where(a => a.AuctionStatus == AuctionStatus.ACTIVE && a.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var auc in activeAuctions)
        {
            if (auc.AuctionType == AuctionType.ENGLISH)
            {
                await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
                {
                    AuctionId = auc.Id,
                    JobType = AuctionDelayJobTypes.AUCTION_END,
                    ExecuteAt = auc.EndAt
                }, cancellationToken);
            }
            else if (auc.AuctionType == AuctionType.DUTCH && auc.AuctionRule != null)
            {
                var interval = auc.AuctionRule.DutchDecreaseIntervalSeconds > 0 ? auc.AuctionRule.DutchDecreaseIntervalSeconds : 60;
                var elapsed = (now - auc.StartAt).TotalSeconds;
                var cycles = Math.Max(0, (int)(elapsed / interval));
                var nextDropAt = auc.StartAt.AddSeconds((cycles + 1) * interval);

                await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
                {
                    AuctionId = auc.Id,
                    JobType = AuctionDelayJobTypes.DUTCH_PRICE_DROP,
                    ExecuteAt = nextDropAt
                }, cancellationToken);
            }
        }

        // Kết quả PENDING_PAYMENT
        var pendingPayments = await context.AuctionResults
            .Where(r => r.AuctionResultStatus == AuctionResultStatus.PENDING_PAYMENT && r.PaymentDeadline.HasValue)
            .ToListAsync(cancellationToken);

        foreach (var res in pendingPayments)
        {
            await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
            {
                AuctionId = res.AuctionId,
                JobType = AuctionDelayJobTypes.PAYMENT_DEADLINE,
                ExecuteAt = res.PaymentDeadline!.Value
            }, cancellationToken);
        }

        // Kết quả RUNNER_UP_PENDING
        var pendingRunnerUps = await context.AuctionResults
            .Where(r => r.AuctionResultStatus == AuctionResultStatus.RUNNER_UP_PENDING && r.RunnerUpPaymentDeadline.HasValue)
            .ToListAsync(cancellationToken);

        foreach (var res in pendingRunnerUps)
        {
            await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
            {
                AuctionId = res.AuctionId,
                JobType = AuctionDelayJobTypes.RUNNER_UP_DEADLINE,
                ExecuteAt = res.RunnerUpPaymentDeadline!.Value
            }, cancellationToken);
        }

        // Kết quả PENDING_SELLER_CONFIRMATION
        var pendingSellerDecisions = await context.AuctionResults
            .Where(r => r.AuctionResultStatus == AuctionResultStatus.PENDING_SELLER_CONFIRMATION && r.SellerDecisionDeadline.HasValue)
            .ToListAsync(cancellationToken);

        foreach (var res in pendingSellerDecisions)
        {
            await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
            {
                AuctionId = res.AuctionId,
                JobType = AuctionDelayJobTypes.SELLER_DECISION_DEADLINE,
                ExecuteAt = res.SellerDecisionDeadline!.Value
            }, cancellationToken);
        }

        _logger.LogInformation("[AuctionDelayDispatcher] Đồng bộ Startup hoàn tất ({SchedCount} scheduled, {ActCount} active, {PayCount} pending payments, {SellerCount} pending seller).",
            scheduledAuctions.Count, activeAuctions.Count, pendingPayments.Count, pendingSellerDecisions.Count);
    }

    private async Task ProcessJobAsync(AuctionDelayJob job, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrustSimDbContext>();
        var realtimeNotifier = scope.ServiceProvider.GetRequiredService<IAuctionRealtimeNotifier>();
        var ledgerService = scope.ServiceProvider.GetRequiredService<ILedgerService>();
        var redisCache = scope.ServiceProvider.GetService<IRedisAuctionCacheService>();
        var notificationService = scope.ServiceProvider.GetService<INotificationService>();

        switch (job.JobType)
        {
            case AuctionDelayJobTypes.AUCTION_START:
                await HandleAuctionStartAsync(job, context, realtimeNotifier, ledgerService, redisCache, notificationService, cancellationToken);
                break;

            case AuctionDelayJobTypes.DUTCH_PRICE_DROP:
                await HandleDutchPriceDropAsync(job, context, realtimeNotifier, ledgerService, notificationService, cancellationToken);
                break;

            case AuctionDelayJobTypes.AUCTION_END:
                await HandleAuctionEndAsync(job, context, realtimeNotifier, ledgerService, redisCache, notificationService, cancellationToken);
                break;

            case AuctionDelayJobTypes.PAYMENT_DEADLINE:
                await HandlePaymentDeadlineAsync(job, context, realtimeNotifier, ledgerService, notificationService, cancellationToken);
                break;

            case AuctionDelayJobTypes.RUNNER_UP_DEADLINE:
                await HandleRunnerUpDeadlineAsync(job, context, realtimeNotifier, notificationService, cancellationToken);
                break;

            case AuctionDelayJobTypes.SELLER_DECISION_DEADLINE:
                await HandleSellerDecisionDeadlineAsync(job, context, realtimeNotifier, ledgerService, notificationService, cancellationToken);
                break;

            case AuctionDelayJobTypes.SIGNING_DEADLINE:
                await HandleSigningDeadlineAsync(job, context, realtimeNotifier, ledgerService, notificationService, cancellationToken);
                break;

            case AuctionDelayJobTypes.TRANSFER_DEADLINE:
                await HandleTransferDeadlineAsync(job, context, realtimeNotifier, ledgerService, notificationService, cancellationToken);
                break;

            case AuctionDelayJobTypes.LIQUIDATION_DEADLINE:
                await HandleLiquidationDeadlineAsync(job, context, realtimeNotifier, ledgerService, notificationService, cancellationToken);
                break;

            default:
                _logger.LogWarning("[AuctionDelayDispatcher] Không hỗ trợ JobType '{JobType}'", job.JobType);
                break;
        }
    }

    private async Task HandleAuctionStartAsync(
        AuctionDelayJob job,
        TrustSimDbContext context,
        IAuctionRealtimeNotifier realtimeNotifier,
        ILedgerService ledgerService,
        IRedisAuctionCacheService? redisCache,
        INotificationService? notificationService,
        CancellationToken cancellationToken)
    {
        var auction = await context.Auctions
            .Include(a => a.AuctionRule)
            .Include(a => a.SimNumber)
            .Include(a => a.Participants)
                .ThenInclude(p => p.Deposits)
            .Include(a => a.Participants)
                .ThenInclude(p => p.UserAccount)
                    .ThenInclude(u => u!.Wallet)
            .FirstOrDefaultAsync(a => a.Id == job.AuctionId, cancellationToken);

        if (auction == null || auction.AuctionStatus != AuctionStatus.SCHEDULE)
            return;

        var now = DateTime.UtcNow;

        // KIỂM TRA 1: PHIÊN BẮT ĐẦU MÀ KHÔNG CÓ AI ĐĂNG KÝ THAM GIA -> HỦY THẤT BẠI
        if (auction.Participants.Count == 0)
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
            await context.AuctionResults.AddAsync(failedResult, cancellationToken);

            // Hoàn cọc cho Người bán nếu có
            if (auction.AuctionRule?.SellerDepositAmount > 0)
            {
                var sellerWallet = await context.Wallets.FirstOrDefaultAsync(w => w.UserAccountId == auction.AuthorId, cancellationToken);
                if (sellerWallet != null)
                {
                    var sellerDeposit = auction.AuctionRule.SellerDepositAmount;
                    var relJournal = await ledgerService.PostSellerListingDepositReleaseAsync(
                        sellerWallet.Id,
                        sellerDeposit,
                        auction.Id,
                        cancellationToken);

                    var sellerRelTx = new AuctionTransaction
                    {
                        Id = Guid.NewGuid(),
                        WalletId = sellerWallet.Id,
                        AuctionId = auction.Id,
                        AuctionTransactionType = AuctionTransactionType.AUCTION_DEPOSIT_RELEASED,
                        TransactionStatus = TransactionStatus.COMPLETED,
                        Amount = sellerDeposit,
                        BalanceBefore = sellerWallet.AvailableBalance - sellerDeposit,
                        BalanceAfter = sellerWallet.AvailableBalance,
                        LedgerJournalId = relJournal.Id,
                        JournalCode = relJournal.JournalCode,
                        CreatedAt = now,
                        CompletedAt = now
                    };
                    await context.AuctionTransactions.AddAsync(sellerRelTx, cancellationToken);
                }
            }

            await context.SaveChangesAsync(cancellationToken);

            if (notificationService != null)
            {
                await notificationService.SendNotificationAsync(
                    userId: auction.AuthorId,
                    type: NotificationType.AUCTION_FAILED,
                    title: $"ℹ️ Phiên đấu giá SIM {auction.SimNumber?.PhoneNumber} bị hủy",
                    content: $"Phiên đấu giá {auction.AuctionCode} đã đến giờ bắt đầu nhưng không có người đăng ký tham gia. Phiên đã tự động hủy và SIM được chuyển về trạng thái Sẵn sàng.",
                    referenceType: "Auction",
                    referenceId: auction.Id,
                    sendEmail: true,
                    cancellationToken: cancellationToken);
            }

            await realtimeNotifier.NotifyAuctionStatusChangedAsync(
                auction.Id,
                AuctionResultStatus.FAILED.ToString(),
                null,
                auction.StartingPrice,
                cancellationToken);

            await realtimeNotifier.NotifyAuctionFailedAsync(
                auction.Id,
                "Phiên đấu giá bắt đầu nhưng không có người đăng ký tham gia đặt cọc.",
                cancellationToken);

            _logger.LogInformation("[AuctionDelayDispatcher] ⛔ Phiên '{Code}' (ID: {Id}) bị hủy do không có người tham gia.",
                auction.AuctionCode, auction.Id);
            return;
        }

        auction.AuctionStatus = AuctionStatus.ACTIVE;
        auction.LockVersion++;

        // Đóng băng nạp/rút của người chơi trong phiên
        foreach (var participant in auction.Participants)
        {
            var wallet = participant.UserAccount?.Wallet;
            if (wallet != null && wallet.WalletStatus == WalletStatus.ACTIVE)
            {
                wallet.WalletStatus = WalletStatus.FROZEN;
                wallet.LockVersion++;
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        // Nạp trước dữ liệu vào Redis In-Memory Hot Cache
        if (redisCache != null)
        {
            await redisCache.WarmUpAuctionAsync(auction, cancellationToken);
        }

        // Bắn SignalR thông báo phiên mở live
        await realtimeNotifier.NotifyAuctionStatusChangedAsync(
            auction.Id,
            AuctionStatus.ACTIVE.ToString(),
            null,
            auction.CurrentPrice,
            cancellationToken);

        _logger.LogInformation("[AuctionDelayDispatcher] 🟢 Phiên '{Code}' (ID: {Id}) đã mở phòng LIVE (ACTIVE).",
            auction.AuctionCode, auction.Id);

        // Lập lịch tiếp theo
        if (auction.AuctionType == AuctionType.DUTCH && auction.AuctionRule != null)
        {
            var interval = auction.AuctionRule.DutchDecreaseIntervalSeconds > 0 ? auction.AuctionRule.DutchDecreaseIntervalSeconds : 60;
            var nextDropAt = auction.StartAt.AddSeconds(interval);

            await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
            {
                AuctionId = auction.Id,
                JobType = AuctionDelayJobTypes.DUTCH_PRICE_DROP,
                ExecuteAt = nextDropAt
            }, cancellationToken);
        }
        else if (auction.AuctionType == AuctionType.ENGLISH)
        {
            await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
            {
                AuctionId = auction.Id,
                JobType = AuctionDelayJobTypes.AUCTION_END,
                ExecuteAt = auction.EndAt
            }, cancellationToken);
        }
    }

    private async Task HandleDutchPriceDropAsync(
        AuctionDelayJob job,
        TrustSimDbContext context,
        IAuctionRealtimeNotifier realtimeNotifier,
        ILedgerService ledgerService,
        INotificationService? notificationService,
        CancellationToken cancellationToken)
    {
        var auction = await context.Auctions
            .Include(a => a.AuctionRule)
            .Include(a => a.SimNumber)
            .Include(a => a.Participants)
                .ThenInclude(p => p.Deposits)
            .Include(a => a.Participants)
                .ThenInclude(p => p.UserAccount)
                    .ThenInclude(u => u!.Wallet)
            .FirstOrDefaultAsync(a => a.Id == job.AuctionId, cancellationToken);

        if (auction == null || auction.AuctionStatus != AuctionStatus.ACTIVE || auction.AuctionRule == null)
            return;

        var rule = auction.AuctionRule;
        var interval = rule.DutchDecreaseIntervalSeconds > 0 ? rule.DutchDecreaseIntervalSeconds : 60;
        var now = DateTime.UtcNow;
        var elapsed = (now - auction.StartAt).TotalSeconds;
        var cycles = Math.Max(0, (int)(elapsed / interval));
        var calculatedPrice = auction.StartingPrice - (cycles * rule.DutchDecreaseAmount);
        var newCurrentPrice = Math.Max(calculatedPrice, auction.ReservePrice);

        auction.CurrentPrice = newCurrentPrice;
        auction.LockVersion++;

        // Kiểm tra nếu chạm giá sàn và không ai mua sau 1 chu kỳ chờ
        if (newCurrentPrice <= auction.ReservePrice && (now - auction.EndAt).TotalSeconds >= 0)
        {
            // Kết thúc Thất bại (FAILED)
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
            await context.AuctionResults.AddAsync(result, cancellationToken);

            // Hoàn cọc & ghi nhận Sổ cái, unfreeze ví toàn bộ người tham gia
            foreach (var participant in auction.Participants)
            {
                participant.Status = ParticipantStatus.REFUNDED;
                var wallet = participant.UserAccount?.Wallet;
                var deposit = participant.Deposits.FirstOrDefault(d => d.DepositStatus == DepositStatus.LOCKED);

                if (wallet != null && deposit != null)
                {
                    deposit.DepositStatus = DepositStatus.RELEASED;
                    deposit.ReleasedAT = now;

                    var relJournal = await ledgerService.PostAuctionDepositReleaseAsync(
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
                    await context.AuctionTransactions.AddAsync(refundTx, cancellationToken);

                    if (notificationService != null)
                    {
                        await notificationService.SendNotificationAsync(
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

            // Hoàn cọc cho Người bán khi phiên Hà Lan chạm sàn không ai mua
            if (auction.AuctionRule?.SellerDepositAmount > 0)
            {
                var sellerWallet = await context.Wallets.FirstOrDefaultAsync(w => w.UserAccountId == auction.AuthorId, cancellationToken);
                if (sellerWallet != null)
                {
                    var sellerDeposit = auction.AuctionRule.SellerDepositAmount;
                    var relJournal = await ledgerService.PostSellerListingDepositReleaseAsync(
                        sellerWallet.Id,
                        sellerDeposit,
                        auction.Id,
                        cancellationToken);

                    var sellerRelTx = new AuctionTransaction
                    {
                        Id = Guid.NewGuid(),
                        WalletId = sellerWallet.Id,
                        AuctionId = auction.Id,
                        AuctionTransactionType = AuctionTransactionType.AUCTION_DEPOSIT_RELEASED,
                        TransactionStatus = TransactionStatus.COMPLETED,
                        Amount = sellerDeposit,
                        BalanceBefore = sellerWallet.AvailableBalance - sellerDeposit,
                        BalanceAfter = sellerWallet.AvailableBalance,
                        LedgerJournalId = relJournal.Id,
                        JournalCode = relJournal.JournalCode,
                        CreatedAt = now,
                        CompletedAt = now
                    };
                    await context.AuctionTransactions.AddAsync(sellerRelTx, cancellationToken);
                }
            }

            await context.SaveChangesAsync(cancellationToken);

            if (notificationService != null)
            {
                await notificationService.SendNotificationAsync(
                    userId: auction.AuthorId,
                    type: NotificationType.AUCTION_FAILED,
                    title: $"ℹ️ Phiên đấu giá SIM {auction.SimNumber?.PhoneNumber} kết thúc thất bại",
                    content: $"Phiên đấu giá Hà Lan {auction.AuctionCode} đã kết thúc mà không có người mua. SIM đã được chuyển về trạng thái Sẵn sàng.",
                    referenceType: "Auction",
                    referenceId: auction.Id,
                    sendEmail: true,
                    cancellationToken: cancellationToken);
            }

            await realtimeNotifier.NotifyAuctionStatusChangedAsync(
                auction.Id,
                AuctionResultStatus.FAILED.ToString(),
                null,
                auction.CurrentPrice,
                cancellationToken);

            await realtimeNotifier.NotifyAuctionFailedAsync(auction.Id, "Phiên đấu giá Hà Lan đã chạm giá sàn mà không có người mua.", cancellationToken);
            return;
        }

        await context.SaveChangesAsync(cancellationToken);

        // Bắn SignalR hạ giá
        var nextDropInSeconds = (int)Math.Max(0, ((cycles + 1) * interval) - elapsed);
        await realtimeNotifier.NotifyPriceDropAsync(auction.Id, newCurrentPrice, nextDropInSeconds, cancellationToken);

        // Enqueue nhịp tiếp theo nếu chưa chạm sàn hoặc chưa hết giờ
        var nextDropAt = auction.StartAt.AddSeconds((cycles + 1) * interval);
        await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
        {
            AuctionId = auction.Id,
            JobType = AuctionDelayJobTypes.DUTCH_PRICE_DROP,
            ExecuteAt = nextDropAt
        }, cancellationToken);
    }

    private async Task HandleAuctionEndAsync(
        AuctionDelayJob job,
        TrustSimDbContext context,
        IAuctionRealtimeNotifier realtimeNotifier,
        ILedgerService ledgerService,
        IRedisAuctionCacheService? redisCache,
        INotificationService? notificationService,
        CancellationToken cancellationToken)
    {
        var auction = await context.Auctions
            .Include(a => a.AuctionRule)
            .Include(a => a.SimNumber)
            .Include(a => a.Bids)
            .Include(a => a.Participants)
                .ThenInclude(p => p.Deposits)
            .Include(a => a.Participants)
                .ThenInclude(p => p.UserAccount)
                    .ThenInclude(u => u!.Wallet)
            .FirstOrDefaultAsync(a => a.Id == job.AuctionId, cancellationToken);

        if (auction == null || auction.AuctionStatus != AuctionStatus.ACTIVE)
            return;

        var now = DateTime.UtcNow;
        auction.AuctionStatus = AuctionStatus.END;
        auction.IsActive = false;
        auction.LockVersion++;

        // Thiết lập TTL 24h dọn dẹp Hot Cache trên Redis RAM
        if (redisCache != null)
        {
            await redisCache.EvictAuctionCacheWithTtlAsync(auction.Id, TimeSpan.FromHours(24), cancellationToken);
        }

        // Tìm Top 1 Winning Bid và Top 2 Runner-Up Bid
        var acceptedBids = auction.Bids
            .Where(b => b.Status == BidStatus.ACCEPTED || b.Status == BidStatus.WINNING || b.Status == BidStatus.OUTBIB)
            .OrderByDescending(b => b.BidAmount)
            .ToList();

        var topBid = acceptedBids.FirstOrDefault();
        var runnerUpBid = acceptedBids.Skip(1).FirstOrDefault();
        var runnerUpParticipant = runnerUpBid != null ? auction.Participants.FirstOrDefault(p => p.Id == runnerUpBid.ParticipantId) : null;

        if (topBid == null)
        {
            // Không có bid nào -> FAILED
            if (auction.SimNumber != null)
                auction.SimNumber.SimStatus = SimStatus.AVAILABLE;

            var noWinnerResult = new AuctionResult
            {
                Id = Guid.NewGuid(),
                AuctionId = auction.Id,
                WinnerId = null,
                FinalPrice = auction.StartingPrice,
                AuctionResultStatus = AuctionResultStatus.FAILED,
                CancelledAt = now,
                CancelledReason = "Phiên đấu giá kết thúc không có người tham gia đặt giá. Toàn bộ tiền cọc đã được hoàn trả.",
                DeterminedAt = now,
                ConfirmedAt = now
            };
            await context.AuctionResults.AddAsync(noWinnerResult, cancellationToken);

            // Hoàn cọc & mở ví người tham gia kèm ghi sổ cái
            foreach (var p in auction.Participants)
            {
                p.Status = ParticipantStatus.REFUNDED;
                var w = p.UserAccount?.Wallet;
                var dep = p.Deposits.FirstOrDefault(d => d.DepositStatus == DepositStatus.LOCKED);

                if (w != null && dep != null)
                {
                    dep.DepositStatus = DepositStatus.RELEASED;
                    dep.ReleasedAT = now;

                    var relJournal = await ledgerService.PostAuctionDepositReleaseAsync(
                        w.Id,
                        dep.DepositAmount,
                        dep.Id,
                        cancellationToken);

                    var refundTx = new AuctionTransaction
                    {
                        Id = Guid.NewGuid(),
                        WalletId = w.Id,
                        AuctionId = auction.Id,
                        AuctionDeposit = dep,
                        AuctionTransactionType = AuctionTransactionType.AUCTION_DEPOSIT_RELEASED,
                        TransactionStatus = TransactionStatus.COMPLETED,
                        Amount = dep.DepositAmount,
                        BalanceBefore = w.AvailableBalance,
                        BalanceAfter = w.AvailableBalance,
                        LedgerJournalId = relJournal.Id,
                        JournalCode = relJournal.JournalCode,
                        CreatedAt = now,
                        CompletedAt = now
                    };
                    await context.AuctionTransactions.AddAsync(refundTx, cancellationToken);

                    // Gửi thông báo hoàn cọc cho từng người tham gia
                    if (notificationService != null)
                    {
                        await notificationService.SendNotificationAsync(
                            userId: p.UserAccountId,
                            type: NotificationType.AUCTION_DEPOSIT_REFUNDED,
                            title: $"ℹ️ Hoàn tiền cọc phiên {auction.AuctionCode}",
                            content: $"Phiên đấu giá SIM {auction.SimNumber?.PhoneNumber} kết thúc mà không có lượt đặt giá nào. Tiền cọc {dep.DepositAmount:N0} VNĐ của bạn đã được hoàn trả về ví khả dụng.",
                            referenceType: "Auction",
                            referenceId: auction.Id,
                            sendEmail: true,
                            cancellationToken: cancellationToken);
                    }
                }

                if (w != null)
                {
                    w.WalletStatus = WalletStatus.ACTIVE;
                }
            }

            // Hoàn cọc cho Người bán khi phiên kết thúc không có ai đặt giá
            if (auction.AuctionRule?.SellerDepositAmount > 0)
            {
                var sellerWallet = await context.Wallets.FirstOrDefaultAsync(w => w.UserAccountId == auction.AuthorId, cancellationToken);
                if (sellerWallet != null)
                {
                    var sellerDeposit = auction.AuctionRule.SellerDepositAmount;
                    var relJournal = await ledgerService.PostSellerListingDepositReleaseAsync(
                        sellerWallet.Id,
                        sellerDeposit,
                        auction.Id,
                        cancellationToken);

                    var sellerRelTx = new AuctionTransaction
                    {
                        Id = Guid.NewGuid(),
                        WalletId = sellerWallet.Id,
                        AuctionId = auction.Id,
                        AuctionTransactionType = AuctionTransactionType.AUCTION_DEPOSIT_RELEASED,
                        TransactionStatus = TransactionStatus.COMPLETED,
                        Amount = sellerDeposit,
                        BalanceBefore = sellerWallet.AvailableBalance - sellerDeposit,
                        BalanceAfter = sellerWallet.AvailableBalance,
                        LedgerJournalId = relJournal.Id,
                        JournalCode = relJournal.JournalCode,
                        CreatedAt = now,
                        CompletedAt = now
                    };
                    await context.AuctionTransactions.AddAsync(sellerRelTx, cancellationToken);
                }
            }

            await context.SaveChangesAsync(cancellationToken);

            // Gửi thông báo cho Người bán
            if (notificationService != null)
            {
                await notificationService.SendNotificationAsync(
                    userId: auction.AuthorId,
                    type: NotificationType.AUCTION_FAILED,
                    title: $"ℹ️ Phiên đấu giá SIM {auction.SimNumber?.PhoneNumber} kết thúc thất bại",
                    content: $"Phiên đấu giá {auction.AuctionCode} đã kết thúc nhưng không có lượt đặt giá nào. SIM đã được chuyển về trạng thái Sẵn sàng.",
                    referenceType: "Auction",
                    referenceId: auction.Id,
                    sendEmail: true,
                    cancellationToken: cancellationToken);
            }

            await realtimeNotifier.NotifyAuctionStatusChangedAsync(
                auction.Id,
                AuctionResultStatus.FAILED.ToString(),
                null,
                auction.StartingPrice,
                cancellationToken);

            await realtimeNotifier.NotifyAuctionFailedAsync(auction.Id, "Phiên đấu giá kết thúc không có người tham gia đặt giá.", cancellationToken);
            return;
        }

        // Có người thắng
        var winnerParticipant = auction.Participants.FirstOrDefault(p => p.Id == topBid.ParticipantId);
        topBid.Status = BidStatus.WINNING;

        var graceMinutes = auction.AuctionRule?.PaymentGracePeriodMinutes > 0
            ? auction.AuctionRule.PaymentGracePeriodMinutes
            : (auction.AuctionRule?.PaymentGracePeriodHours > 0 ? auction.AuctionRule.PaymentGracePeriodHours * 60 : 1440);
        var paymentDeadline = now.AddMinutes(graceMinutes);

        var resultEntity = new AuctionResult
        {
            Id = Guid.NewGuid(),
            AuctionId = auction.Id,
            InitialWinnerId = winnerParticipant?.Id,
            InitialWinningPrice = topBid.BidAmount,
            WinnerId = winnerParticipant?.Id,
            FinalPrice = topBid.BidAmount,
            RunnerUpParticipantId = runnerUpParticipant?.Id,
            RunnerUpFinalPrice = runnerUpBid?.BidAmount,
            WinType = AuctionWinType.NONE,
            AuctionResultStatus = AuctionResultStatus.PENDING_PAYMENT,
            PaymentDeadline = paymentDeadline,
            DeterminedAt = now,
            ConfirmedAt = now
        };
        await context.AuctionResults.AddAsync(resultEntity, cancellationToken);

        // Hoàn cọc và unfreeze ví cho toàn bộ người thua (bao gồm cả Runner-up)
        foreach (var p in auction.Participants)
        {
            if (winnerParticipant != null && p.Id == winnerParticipant.Id)
            {
                p.Status = ParticipantStatus.WON;
                var w = p.UserAccount?.Wallet;
                if (w != null)
                {
                    w.WalletStatus = WalletStatus.ACTIVE;
                }
                continue;
            }

            p.Status = (runnerUpParticipant != null && p.Id == runnerUpParticipant.Id)
                ? ParticipantStatus.RUNNER_UP
                : ParticipantStatus.LOST;

            var wallet = p.UserAccount?.Wallet;
            var dep = p.Deposits.FirstOrDefault(d => d.DepositStatus == DepositStatus.LOCKED);

            if (wallet != null && dep != null)
            {
                dep.DepositStatus = DepositStatus.RELEASED;
                dep.ReleasedAT = now;

                var relJournal = await ledgerService.PostAuctionDepositReleaseAsync(
                    wallet.Id,
                    dep.DepositAmount,
                    dep.Id,
                    cancellationToken);

                var refundTx = new AuctionTransaction
                {
                    Id = Guid.NewGuid(),
                    WalletId = wallet.Id,
                    AuctionId = auction.Id,
                    AuctionDeposit = dep,
                    AuctionTransactionType = AuctionTransactionType.AUCTION_DEPOSIT_RELEASED,
                    TransactionStatus = TransactionStatus.COMPLETED,
                    Amount = dep.DepositAmount,
                    BalanceBefore = wallet.AvailableBalance - dep.DepositAmount,
                    BalanceAfter = wallet.AvailableBalance,
                    LedgerJournalId = relJournal.Id,
                    JournalCode = relJournal.JournalCode,
                    CreatedAt = now,
                    CompletedAt = now
                };
                await context.AuctionTransactions.AddAsync(refundTx, cancellationToken);

                // Bắn thông báo hoàn cọc tự động
                if (notificationService != null)
                {
                    await notificationService.SendNotificationAsync(
                        userId: p.UserAccountId,
                        type: NotificationType.AUCTION_DEPOSIT_REFUNDED,
                        title: $"💵 Hoàn cọc phiên {auction.AuctionCode}",
                        content: $"Phiên đấu giá SIM {auction.SimNumber?.PhoneNumber} đã kết thúc. Khoản tiền cọc {dep.DepositAmount:N0} VNĐ của bạn đã được hoàn trả về ví khả dụng.",
                        referenceType: "Auction",
                        referenceId: auction.Id,
                        cancellationToken: cancellationToken);
                }
            }

            if (wallet != null)
            {
                wallet.WalletStatus = WalletStatus.ACTIVE;
            }
        }

        // Quyết toán Người thắng: Nếu đủ tiền -> Tự động quyết toán ngay; Nếu thiếu -> Chờ thời hạn (Grace Period)
        if (winnerParticipant?.UserAccount?.Wallet != null)
        {
            var winWallet = winnerParticipant.UserAccount.Wallet;
            var winnerDeposit = winnerParticipant.Deposits.FirstOrDefault(d => d.DepositStatus == DepositStatus.LOCKED);
            var depositPaid = winnerDeposit?.DepositAmount ?? 0m;
            var remainingRequired = topBid.BidAmount - depositPaid;

            if (winWallet.AvailableBalance >= remainingRequired)
            {
                // Tự động quyết toán thành công ngay lập tức
                var escrowId = Guid.NewGuid();
                var tradeId = Guid.NewGuid();

                var signingGraceMinutes = auction.AuctionRule?.SigningGracePeriodMinutes ?? 1440;
                var signingDeadline = now.AddMinutes(signingGraceMinutes);

                var trade = new TradeTransaction
                {
                    Id = tradeId,
                    TradeTransactionCode = $"TRD-{now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
                    TradeTransactionStatus = TradeTransactionStatus.PendingSignature,
                    AuctionId = auction.Id,
                    BuyerId = winnerParticipant.UserAccountId,
                    SellerId = auction.AuthorId,
                    SimNumberId = auction.SimNumberId,
                    EscrowDeadline = null,
                    SigningDeadline = signingDeadline,
                    TransferDeadline = null,
                    LiquidationDeadline = null,
                    CompletedAt = null,
                    CancelledAt = null,
                    IsActive = true
                };

                var contract = new TransferContract
                {
                    Id = Guid.NewGuid(),
                    TradeTransactionId = trade.Id,
                    ContractStatus = ContractStatus.DRAFT,
                    DocumentUrl = string.Empty,
                    DocumentHash = string.Empty
                };
                trade.TransferContract = contract;

                var escrow = new Escrow
                {
                    Id = escrowId,
                    TradeTransactionId = trade.Id,
                    EscrowAmount = topBid.BidAmount,
                    EscrowStatus = EscrowStatus.HELD,
                    AuctionDepositId = winnerDeposit?.Id,
                    HeldAt = now
                };

                trade.Escrow = escrow;

                var journal = await ledgerService.PostEscrowHoldAsync(
                    winWallet.Id,
                    topBid.BidAmount,
                    depositPaid,
                    escrowId,
                    cancellationToken);

                if (winnerDeposit != null)
                {
                    winnerDeposit.DepositStatus = DepositStatus.APPLIED_TO_ESCROW;
                    winnerDeposit.AppliedtoEscrowedAt = now;
                    winnerDeposit.Escrow = escrow;
                }

                var auctionTx = new AuctionTransaction
                {
                    Id = Guid.NewGuid(),
                    WalletId = winWallet.Id,
                    AuctionId = auction.Id,
                    EscrowId = escrowId,
                    AuctionTransactionType = AuctionTransactionType.ESCROW_HOLD,
                    TransactionStatus = TransactionStatus.COMPLETED,
                    Amount = topBid.BidAmount,
                    BalanceBefore = winWallet.AvailableBalance + remainingRequired,
                    BalanceAfter = winWallet.AvailableBalance,
                    LedgerJournalId = journal.Id,
                    JournalCode = journal.JournalCode,
                    CreatedAt = now,
                    CompletedAt = now
                };
                await context.AuctionTransactions.AddAsync(auctionTx, cancellationToken);

                if (auction.SimNumber != null)
                {
                    auction.SimNumber.SimStatus = SimStatus.TRANSFERRING;
                }

                winWallet.WalletStatus = WalletStatus.ACTIVE;
                resultEntity.AuctionResultStatus = AuctionResultStatus.CONFIRMED;
                resultEntity.WinType = AuctionWinType.ORIGINAL_WINNER;
                resultEntity.SettledAt = now;

                await context.Escrows.AddAsync(escrow, cancellationToken);
                await context.TradeTransactions.AddAsync(trade, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);

                // Nạp deadline ký hợp đồng vào Delay Queue
                await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
                {
                    AuctionId = auction.Id,
                    JobType = AuctionDelayJobTypes.SIGNING_DEADLINE,
                    ExecuteAt = trade.SigningDeadline!.Value,
                    ExtraData = trade.Id.ToString()
                }, cancellationToken);

                // Tự động sinh Văn bản 1 (Phiếu xác nhận kết quả đấu giá) và upload Supabase Storage CDN ngay lập tức
                string? winningNoticeUrl = null;
                try
                {
                    using var pdfScope = _scopeFactory.CreateScope();
                    var pdfService = pdfScope.ServiceProvider.GetService<IPdfContractService>();
                    if (pdfService != null)
                    {
                        winningNoticeUrl = await pdfService.EnsureWinningNoticeUploadedAsync(trade.Id, cancellationToken);
                        _logger.LogInformation("[AuctionDelayDispatcher] 📄 Đã tạo và lưu Phiếu chốt giá trúng trên Supabase CDN: {Url}", winningNoticeUrl);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[AuctionDelayDispatcher] Lỗi khi tạo Phiếu chốt giá trúng cho Trade {TradeCode}", trade.TradeTransactionCode);
                }

                var winnerName = winnerParticipant.UserAccount?.UserName ?? "Người tham gia";
                var graceHours = auction.AuctionRule?.SigningGracePeriodHours ?? 24;

                if (notificationService != null)
                {
                    await notificationService.SendNotificationAsync(
                        userId: winnerParticipant.UserAccountId,
                        type: NotificationType.CONTRACT_SIGNING_REQUIRED,
                        title: $"📝 Yêu cầu ký hợp đồng SIM {auction.SimNumber?.PhoneNumber}!",
                        content: $"Chúc mừng bạn đã trúng đấu giá SIM {auction.SimNumber?.PhoneNumber} với mức giá {topBid.BidAmount:N0} VNĐ (Đã thanh toán Escrow). Vui lòng xem biên bản chốt giá trúng và ký hợp đồng điện tử trong vòng {graceHours} giờ.",
                        actionUrl: winningNoticeUrl ?? $"/trades/{trade.Id}",
                        referenceType: "TradeTransaction",
                        referenceId: trade.Id,
                        sendEmail: true,
                        cancellationToken: cancellationToken);

                    await notificationService.SendNotificationAsync(
                        userId: auction.AuthorId,
                        type: NotificationType.CONTRACT_SIGNING_REQUIRED,
                        title: $"📝 Người thắng {winnerName} đã thanh toán! Vui lòng ký hợp đồng",
                        content: $"Phiên {auction.AuctionCode} đã kết thúc. Người thắng ({winnerName}) đã thanh toán đủ {topBid.BidAmount:N0} VNĐ vào Escrow. Vui lòng xem biên bản chốt giá trúng và ký hợp đồng điện tử trong vòng {graceHours} giờ.",
                        actionUrl: winningNoticeUrl ?? $"/trades/{trade.Id}",
                        referenceType: "TradeTransaction",
                        referenceId: trade.Id,
                        sendEmail: true,
                        cancellationToken: cancellationToken);
                }
            }
            else
            {
                // Người thắng chưa đủ 100% tiền thanh toán -> Thu ngay phần cọc lũy tiến còn thiếu Delta
                var marginRate = auction.AuctionRule?.MarginPercentage ?? 0.10m;
                var requiredMarginTotal = Math.Round(topBid.BidAmount * marginRate, 2);
                var deltaMargin = Math.Max(0, requiredMarginTotal - depositPaid);

                if (deltaMargin > 0 && winWallet.AvailableBalance > 0)
                {
                    // Thu tối đa số dư khả dụng hiện có (lên đến deltaMargin)
                    var actualHoldAmount = Math.Min(winWallet.AvailableBalance, deltaMargin);

                    var addDepositId = Guid.NewGuid();
                    var depCode = $"DEP-ADD-{now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";

                    // Ghi sổ cái khóa tiền cọc bổ sung qua method chuyên biệt
                    var holdJournal = await ledgerService.PostAuctionProgressiveDepositLockAsync(
                        winWallet.Id,
                        actualHoldAmount,
                        addDepositId,
                        auction.Id,
                        cancellationToken);

                    var progressiveDeposit = new AuctionDeposit
                    {
                        Id = addDepositId,
                        ParticipantId = winnerParticipant.Id,
                        DepositCode = depCode,
                        DepositAmount = actualHoldAmount,
                        DepositType = DepositType.PROGRESSIVE_ADDITIONAL,
                        DepositStatus = DepositStatus.LOCKED,
                        AuctionTransactionId = Guid.NewGuid()
                    };

                    var lockTx = new AuctionTransaction
                    {
                        Id = progressiveDeposit.AuctionTransactionId,
                        WalletId = winWallet.Id,
                        AuctionId = auction.Id,
                        AuctionDeposit = progressiveDeposit,
                        AuctionTransactionType = AuctionTransactionType.AUCTION_PROGRESSIVE_DEPOSIT_LOCK,
                        TransactionStatus = TransactionStatus.COMPLETED,
                        Amount = actualHoldAmount,
                        BalanceBefore = winWallet.AvailableBalance + actualHoldAmount,
                        BalanceAfter = winWallet.AvailableBalance,
                        LedgerJournalId = holdJournal.Id,
                        JournalCode = holdJournal.JournalCode,
                        CreatedAt = now,
                        CompletedAt = now
                    };

                    await context.AuctionDeposits.AddAsync(progressiveDeposit, cancellationToken);
                    await context.AuctionTransactions.AddAsync(lockTx, cancellationToken);

                    _logger.LogInformation(
                        "[AuctionDelayDispatcher] 🔒 Đã thu cọc bổ sung lũy tiến {Amount:N0} VNĐ từ Winner {User} cho phiên {Auction}",
                        actualHoldAmount, winnerParticipant.UserAccount?.UserName, auction.AuctionCode);
                }

                winWallet.WalletStatus = WalletStatus.ACTIVE;
                resultEntity.AuctionResultStatus = AuctionResultStatus.PENDING_PAYMENT;
                resultEntity.WinType = AuctionWinType.NONE;
                resultEntity.PaymentDeadline = paymentDeadline;

                await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
                {
                    AuctionId = auction.Id,
                    JobType = AuctionDelayJobTypes.PAYMENT_DEADLINE,
                    ExecuteAt = paymentDeadline
                }, cancellationToken);

                var winnerName = winnerParticipant.UserAccount?.UserName ?? "Người tham gia";

                if (notificationService != null)
                {
                    await notificationService.SendNotificationAsync(
                        userId: winnerParticipant.UserAccountId,
                        type: NotificationType.AUCTION_WON,
                        title: $"🏆 Bạn đã thắng đấu giá SIM {auction.SimNumber?.PhoneNumber}!",
                        content: $"Mức giá trúng của bạn là {topBid.BidAmount:N0} VNĐ. Vui lòng nạp thêm và hoàn tất thanh toán trước {paymentDeadline.ToVietnamTimeString("HH:mm dd/MM/yyyy")}.",
                        actionUrl: $"/auctions/{auction.Id}/payment",
                        referenceType: "Auction",
                        referenceId: auction.Id,
                        sendEmail: true,
                        cancellationToken: cancellationToken);

                    await notificationService.SendNotificationAsync(
                        userId: auction.AuthorId,
                        type: NotificationType.SYSTEM_ALERT,
                        title: $"ℹ️ Phiên SIM {auction.SimNumber?.PhoneNumber} đã kết thúc (Chờ thanh toán)",
                        content: $"Phiên đấu giá {auction.AuctionCode} đã kết thúc. Người thắng là {winnerName} với mức giá {topBid.BidAmount:N0} VNĐ (Chưa thanh toán - Hạn thanh toán đến {paymentDeadline.ToVietnamTimeString("HH:mm dd/MM/yyyy")}). Hệ thống sẽ tự động thông báo khi người thắng thanh toán hoặc quá hạn.",
                        referenceType: "Auction",
                        referenceId: auction.Id,
                        sendEmail: true,
                        cancellationToken: cancellationToken);
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        var resultDto = new AuctionResultDto
        {
            Id = resultEntity.Id,
            AuctionId = resultEntity.AuctionId,
            WinnerId = resultEntity.WinnerId,
            FinalPrice = resultEntity.FinalPrice,
            InitialWinnerId = resultEntity.InitialWinnerId,
            InitialWinningPrice = resultEntity.InitialWinningPrice,
            RunnerUpParticipantId = resultEntity.RunnerUpParticipantId,
            RunnerUpFinalPrice = resultEntity.RunnerUpFinalPrice,
            WinType = resultEntity.WinType,
            AuctionResultStatus = resultEntity.AuctionResultStatus,
            PaymentDeadline = resultEntity.PaymentDeadline,
            SettledAt = resultEntity.SettledAt
        };

        await realtimeNotifier.NotifyAuctionEndedAsync(auction.Id, resultDto, cancellationToken);
        _logger.LogInformation("[AuctionDelayDispatcher] 🏁 Phiên '{Code}' đã kết thúc. Người thắng: {WinnerId}, Giá: {Price:N0} VNĐ",
            auction.AuctionCode, resultEntity.WinnerId, resultEntity.FinalPrice);
    }

    private async Task HandlePaymentDeadlineAsync(
        AuctionDelayJob job,
        TrustSimDbContext context,
        IAuctionRealtimeNotifier realtimeNotifier,
        ILedgerService ledgerService,
        INotificationService? notificationService,
        CancellationToken cancellationToken)
    {
        var result = await context.AuctionResults
            .Include(r => r.Auction)
                .ThenInclude(a => a!.AuctionRule)
            .Include(r => r.Auction)
                .ThenInclude(a => a!.SimNumber)
            .Include(r => r.Winner)
                .ThenInclude(w => w!.Deposits)
            .Include(r => r.Winner)
                .ThenInclude(w => w!.UserAccount)
                    .ThenInclude(u => u!.Wallet)
            .Include(r => r.RunnerUpParticipant)
                .ThenInclude(p => p!.UserAccount)
            .FirstOrDefaultAsync(r => r.AuctionId == job.AuctionId, cancellationToken);

        if (result == null || result.AuctionResultStatus != AuctionResultStatus.PENDING_PAYMENT)
            return;

        var auction = result.Auction;
        if (auction == null) return;

        var now = DateTime.UtcNow;
        decimal compensationAmount = 0m;
        var lockedDeposits = result.Winner?.Deposits
            .Where(d => d.DepositStatus == DepositStatus.LOCKED)
            .ToList() ?? new List<AuctionDeposit>();

        decimal totalDepositAmount = lockedDeposits.Sum(d => d.DepositAmount);

        // 1. Tịch thu tiền cọc và bồi thường 50% Seller / 50% Sàn qua Sổ cái kép
        if (result.Winner != null)
        {
            result.Winner.Status = ParticipantStatus.DEFAULTED;
            var winnerWallet = result.Winner.UserAccount?.Wallet;
            var sellerWallet = await context.Wallets.FirstOrDefaultAsync(w => w.UserAccountId == auction.AuthorId, cancellationToken);

            if (lockedDeposits.Count > 0 && winnerWallet != null && sellerWallet != null)
            {
                compensationAmount = Math.Round(totalDepositAmount * 0.50m, 2);
                result.SellerCompensationAmount = compensationAmount;
                var primaryDepositId = lockedDeposits.First().Id;

                var forfeitJournal = await ledgerService.PostAuctionDepositForfeitedWithCompensationAsync(
                    winnerWallet.Id,
                    sellerWallet.Id,
                    totalDepositAmount,
                    primaryDepositId,
                    auction.Id,
                    cancellationToken);

                foreach (var dep in lockedDeposits)
                {
                    dep.DepositStatus = DepositStatus.FORFEITED;
                    dep.ForfeitedAt = now;
                    dep.ForfeitedReason = "Quá hạn thanh toán tiền mua SIM đấu giá (Bùng cọc).";

                    var forfeitureTx = new AuctionTransaction
                    {
                        Id = Guid.NewGuid(),
                        WalletId = winnerWallet.Id,
                        AuctionId = result.AuctionId,
                        AuctionDeposit = dep,
                        AuctionTransactionType = AuctionTransactionType.AUCTION_DEPOSIT_FORFEITED,
                        TransactionStatus = TransactionStatus.COMPLETED,
                        Amount = dep.DepositAmount,
                        BalanceBefore = winnerWallet.AvailableBalance,
                        BalanceAfter = winnerWallet.AvailableBalance,
                        LedgerJournalId = forfeitJournal.Id,
                        JournalCode = forfeitJournal.JournalCode,
                        CreatedAt = now,
                        CompletedAt = now
                    };
                    await context.AuctionTransactions.AddAsync(forfeitureTx, cancellationToken);
                }

                // Ghi nhận giao dịch nhận 50% đền bù cho Người bán
                if (compensationAmount > 0 && sellerWallet != null)
                {
                    var sellerCompTx = new AuctionTransaction
                    {
                        Id = Guid.NewGuid(),
                        WalletId = sellerWallet.Id,
                        AuctionId = result.AuctionId,
                        AuctionTransactionType = AuctionTransactionType.AUCTION_COMPENSATION,
                        TransactionStatus = TransactionStatus.COMPLETED,
                        Amount = compensationAmount,
                        BalanceBefore = sellerWallet.AvailableBalance - compensationAmount,
                        BalanceAfter = sellerWallet.AvailableBalance,
                        LedgerJournalId = forfeitJournal.Id,
                        JournalCode = forfeitJournal.JournalCode,
                        CreatedAt = now,
                        CompletedAt = now
                    };
                    await context.AuctionTransactions.AddAsync(sellerCompTx, cancellationToken);
                }

                _logger.LogInformation(
                    "[AuctionDelayDispatcher] ⚠️ Tịch thu toàn bộ cọc {Total:N0} VNĐ ({Count} khoản cọc) của Winner {User}. Đền bù Seller {Comp:N0} VNĐ",
                    totalDepositAmount, lockedDeposits.Count, result.Winner?.UserAccount?.UserName, compensationAmount);
            }

            // Cập nhật Bid của Winner cũ sang REJECTED
            var winningBid = await context.Bids.FirstOrDefaultAsync(
                b => b.AuctionId == result.AuctionId && b.ParticipantId == result.WinnerId && b.Status == BidStatus.WINNING,
                cancellationToken);
            if (winningBid != null)
            {
                winningBid.Status = BidStatus.REJECTED;
            }

            // Gửi thông báo tịch thu tiền cọc cho Winner trong mọi trường hợp quá hạn
            if (notificationService != null)
            {
                await notificationService.SendNotificationAsync(
                    userId: result.Winner!.UserAccountId,
                    type: NotificationType.AUCTION_DEPOSIT_FORFEITED,
                    title: $"⚠️ Tịch thu tiền cọc phiên {auction.AuctionCode}",
                    content: $"Bạn đã quá hạn thanh toán tiền mua SIM {auction.SimNumber?.PhoneNumber}. Tổng tiền cọc {totalDepositAmount:N0} VNĐ đã bị tịch thu theo quy chế.",
                    referenceType: "Auction",
                    referenceId: auction.Id,
                    sendEmail: true,
                    cancellationToken: cancellationToken);
            }
        }

        // 2. Bộ lọc liêm chính 3 tầng GapRate & Độc lập
        var rule = auction.AuctionRule;
        var minUniqueBidders = rule?.MinimumUniqueBiddersForRunnerUp ?? 2;
        var maxGapRatio = rule?.MaxPriceGapRatioForRunnerUp ?? 1.50m;
        var requireSellerApproval = rule?.RequireSellerApprovalForRunnerUp ?? true;

        var bids = await context.Bids.Where(b => b.AuctionId == auction.Id).ToListAsync(cancellationToken);
        var uniqueBiddersCount = bids.Select(b => b.ParticipantId).Distinct().Count();
        result.UniqueBiddersCount = uniqueBiddersCount;

        bool isShieldingDetected = false;
        string failureReason = string.Empty;

        if (result.WinType != AuctionWinType.RUNNER_UP_TAKEOVER && result.RunnerUpParticipantId.HasValue && result.RunnerUpFinalPrice.HasValue && result.RunnerUpFinalPrice.Value > 0)
        {
            var gapRatio = result.FinalPrice / result.RunnerUpFinalPrice.Value;
            result.PriceGapRatio = Math.Round(gapRatio, 2);

            if (uniqueBiddersCount < minUniqueBidders)
            {
                isShieldingDetected = true;
                failureReason = $"Không đủ số lượng người đặt giá độc lập (chỉ có {uniqueBiddersCount} người tham gia).";
            }
            else if (gapRatio > maxGapRatio)
            {
                isShieldingDetected = true;
                failureReason = $"Phát hiện dấu hiệu Lá chắn giá: Tỷ lệ chênh lệch giá ({gapRatio:F2}x) > {maxGapRatio:F2}x.";
            }
        }
        else
        {
            failureReason = result.WinType == AuctionWinType.RUNNER_UP_TAKEOVER
                ? "Người về nhì đã chấp nhận quyền mua nhưng quá hạn thanh toán tiền mua SIM."
                : "Phiên đấu giá không có Người về nhì hợp lệ.";
        }

        // 3. Cây quyết định & Phát Thông báo
        if (isShieldingDetected || !result.RunnerUpParticipantId.HasValue || !result.RunnerUpFinalPrice.HasValue)
        {
            result.WinnerId = null;
            result.WinType = AuctionWinType.NONE;
            result.AuctionResultStatus = isShieldingDetected ? AuctionResultStatus.INVALIDATED_SHIELDING : AuctionResultStatus.FAILED;
            result.CancelledAt = now;
            result.CancelledReason = isShieldingDetected ? failureReason : "Người thắng quá hạn thanh toán và không có Người về nhì.";

            if (auction.SimNumber != null)
            {
                auction.SimNumber.SimStatus = SimStatus.AVAILABLE;
            }

            // Hoàn cọc cam kết bán cho Người bán khi Winner bùng kèo và không có Á quân hợp lệ
            if (auction.AuctionRule?.SellerDepositAmount > 0)
            {
                var sellerWallet = await context.Wallets.FirstOrDefaultAsync(w => w.UserAccountId == auction.AuthorId, cancellationToken);
                if (sellerWallet != null)
                {
                    var sellerDeposit = auction.AuctionRule.SellerDepositAmount;
                    var relJournal = await ledgerService.PostSellerListingDepositReleaseAsync(
                        sellerWallet.Id,
                        sellerDeposit,
                        auction.Id,
                        cancellationToken);

                    var sellerRelTx = new AuctionTransaction
                    {
                        Id = Guid.NewGuid(),
                        WalletId = sellerWallet.Id,
                        AuctionId = auction.Id,
                        AuctionTransactionType = AuctionTransactionType.AUCTION_DEPOSIT_RELEASED,
                        TransactionStatus = TransactionStatus.COMPLETED,
                        Amount = sellerDeposit,
                        BalanceBefore = sellerWallet.AvailableBalance - sellerDeposit,
                        BalanceAfter = sellerWallet.AvailableBalance,
                        LedgerJournalId = relJournal.Id,
                        JournalCode = relJournal.JournalCode,
                        CreatedAt = now,
                        CompletedAt = now
                    };
                    await context.AuctionTransactions.AddAsync(sellerRelTx, cancellationToken);
                }
            }

            await context.SaveChangesAsync(cancellationToken);

            if (notificationService != null && compensationAmount > 0)
            {
                await notificationService.SendNotificationAsync(
                    userId: auction.AuthorId,
                    type: NotificationType.AUCTION_FAILED,
                    title: $"💰 Nhận đền bù {compensationAmount:N0}đ phiên {auction.AuctionCode}",
                    content: $"Người thắng phiên {auction.AuctionCode} đã quá hạn thanh toán. Bạn đã nhận {compensationAmount:N0} VNĐ (50% tiền cọc) vào ví. Phiên đấu giá kết thúc không thành công.",
                    referenceType: "Auction",
                    referenceId: auction.Id,
                    sendEmail: true,
                    cancellationToken: cancellationToken);
            }

            await realtimeNotifier.NotifyAuctionFailedAsync(result.AuctionId, result.CancelledReason, cancellationToken);
            return;
        }

        // Nhánh hỏi ý kiến Seller
        if (requireSellerApproval)
        {
            var graceMinutes = rule?.PaymentGracePeriodMinutes > 0
                ? rule.PaymentGracePeriodMinutes
                : (rule?.PaymentGracePeriodHours > 0 ? rule.PaymentGracePeriodHours * 60 : 1440);
            var sellerDeadline = now.AddMinutes(graceMinutes);

            result.WinnerId = null;
            result.WinType = AuctionWinType.NONE;
            result.AuctionResultStatus = AuctionResultStatus.PENDING_SELLER_CONFIRMATION;
            result.SellerDecisionDeadline = sellerDeadline;
            await context.SaveChangesAsync(cancellationToken);

            // Lập lịch Job timeout quyết định của Seller vào Redis Delay Queue
            await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
            {
                AuctionId = result.AuctionId,
                JobType = AuctionDelayJobTypes.SELLER_DECISION_DEADLINE,
                ExecuteAt = sellerDeadline
            }, cancellationToken);

            if (notificationService != null)
            {
                await notificationService.SendNotificationAsync(
                    userId: auction.AuthorId,
                    type: NotificationType.AUCTION_SELLER_CONFIRMATION_REQUIRED,
                    title: $"💰 Nhận đền bù {compensationAmount:N0}đ & Xác nhận bán cho Á quân",
                    content: $"Winner phiên {auction.AuctionCode} đã quá hạn thanh toán. Bạn đã nhận {compensationAmount:N0} VNĐ vào ví. Á quân trả giá {result.RunnerUpFinalPrice:N0} VNĐ. Bạn có đồng ý bán cho Á quân không?",
                    actionUrl: $"/seller/auctions/{auction.Id}/confirm-runner-up",
                    referenceType: "Auction",
                    referenceId: auction.Id,
                    metadata: new { compensation = compensationAmount, runnerUpPrice = result.RunnerUpFinalPrice, deadline = sellerDeadline },
                    sendEmail: true,
                    cancellationToken: cancellationToken);
            }

            await realtimeNotifier.NotifyAuctionStatusChangedAsync(
                result.AuctionId,
                AuctionResultStatus.PENDING_SELLER_CONFIRMATION.ToString(),
                result.RunnerUpParticipantId,
                result.RunnerUpFinalPrice,
                cancellationToken);

            return;
        }

        // Nhánh tự động chuyển cho Runner-Up
        var autoGraceMinutes = rule?.PaymentGracePeriodMinutes > 0
            ? rule.PaymentGracePeriodMinutes
            : (rule?.PaymentGracePeriodHours > 0 ? rule.PaymentGracePeriodHours * 60 : 1440);
        var runnerUpDeadlineAuto = now.AddMinutes(autoGraceMinutes);

        result.WinnerId = null;
        result.WinType = AuctionWinType.NONE;
        result.AuctionResultStatus = AuctionResultStatus.RUNNER_UP_PENDING;
        result.IsRunnerUpOffered = true;
        result.RunnerUpPaymentDeadline = runnerUpDeadlineAuto;
        await context.SaveChangesAsync(cancellationToken);

        await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
        {
            AuctionId = result.AuctionId,
            JobType = AuctionDelayJobTypes.RUNNER_UP_DEADLINE,
            ExecuteAt = runnerUpDeadlineAuto
        }, cancellationToken);

        if (notificationService != null)
        {
            if (result.RunnerUpParticipant != null)
            {
                await notificationService.SendNotificationAsync(
                    userId: result.RunnerUpParticipant.UserAccountId,
                    type: NotificationType.AUCTION_RUNNER_UP_OFFERED,
                    title: $"🎉 Chúc mừng bạn được trao quyền mua SIM {auction.SimNumber?.PhoneNumber}!",
                    content: $"Người thắng phiên {auction.AuctionCode} đã quá hạn thanh toán. Bạn là Á quân với mức giá {result.RunnerUpFinalPrice:N0} VNĐ. Vui lòng thanh toán trước {runnerUpDeadlineAuto.ToVietnamTimeString("HH:mm dd/MM/yyyy")}.",
                    actionUrl: $"/auctions/{auction.Id}/payment",
                    referenceType: "Auction",
                    referenceId: auction.Id,
                    sendEmail: true,
                    cancellationToken: cancellationToken);
            }

            if (compensationAmount > 0)
            {
                await notificationService.SendNotificationAsync(
                    userId: auction.AuthorId,
                    type: NotificationType.SYSTEM_ALERT,
                    title: $"💰 Nhận đền bù {compensationAmount:N0}đ & Chuyển quyền mua cho Á quân",
                    content: $"Người thắng phiên {auction.AuctionCode} đã quá hạn thanh toán. Bạn đã nhận {compensationAmount:N0} VNĐ (50% tiền cọc) vào ví. Hệ thống đang tự động trao quyền mua cho Á quân với giá {result.RunnerUpFinalPrice:N0} VNĐ.",
                    referenceType: "Auction",
                    referenceId: auction.Id,
                    sendEmail: true,
                    cancellationToken: cancellationToken);
            }
        }

        await realtimeNotifier.NotifyAuctionStatusChangedAsync(
            result.AuctionId,
            AuctionResultStatus.RUNNER_UP_PENDING.ToString(),
            result.RunnerUpParticipantId,
            result.RunnerUpFinalPrice,
            cancellationToken);
    }

    private async Task HandleRunnerUpDeadlineAsync(
        AuctionDelayJob job,
        TrustSimDbContext context,
        IAuctionRealtimeNotifier realtimeNotifier,
        INotificationService? notificationService,
        CancellationToken cancellationToken)
    {
        var result = await context.AuctionResults
            .Include(r => r.Auction)
                .ThenInclude(a => a!.SimNumber)
            .Include(r => r.RunnerUpParticipant)
            .FirstOrDefaultAsync(r => r.AuctionId == job.AuctionId, cancellationToken);

        if (result == null || result.AuctionResultStatus != AuctionResultStatus.RUNNER_UP_PENDING)
            return;

        var now = DateTime.UtcNow;
        result.WinnerId = null;
        result.WinType = AuctionWinType.NONE;
        result.AuctionResultStatus = AuctionResultStatus.FAILED;
        result.CancelledAt = now;
        result.CancelledReason = "Người về nhì không hoàn tất thanh toán trong thời hạn quy định. Phiên đấu giá kết thúc thất bại.";

        if (result.RunnerUpParticipant != null)
        {
            result.RunnerUpParticipant.Status = ParticipantStatus.LOST;
        }

        if (result.Auction?.SimNumber != null)
        {
            result.Auction.SimNumber.SimStatus = SimStatus.AVAILABLE;
        }

        await context.SaveChangesAsync(cancellationToken);

        if (notificationService != null)
        {
            if (result.RunnerUpParticipant != null)
            {
                await notificationService.SendNotificationAsync(
                    userId: result.RunnerUpParticipant.UserAccountId,
                    type: NotificationType.AUCTION_FAILED,
                    title: $"⚠️ Quá hạn thanh toán quyền mua SIM {result.Auction?.SimNumber?.PhoneNumber}",
                    content: $"Bạn đã quá hạn thanh toán theo mức giá Á quân. Quyền mua SIM đã bị hủy.",
                    referenceType: "Auction",
                    referenceId: result.AuctionId,
                    sendEmail: true,
                    cancellationToken: cancellationToken);
            }

            if (result.Auction != null)
            {
                await notificationService.SendNotificationAsync(
                    userId: result.Auction.AuthorId,
                    type: NotificationType.AUCTION_FAILED,
                    title: $"ℹ️ Phiên đấu giá {result.Auction.AuctionCode} đã kết thúc",
                    content: $"Á quân đã quá hạn thanh toán mua SIM {result.Auction.SimNumber?.PhoneNumber}. Phiên đấu giá kết thúc và SIM đã trở lại trạng thái khả dụng.",
                    referenceType: "Auction",
                    referenceId: result.AuctionId,
                    sendEmail: true,
                    cancellationToken: cancellationToken);
            }
        }

        await realtimeNotifier.NotifyAuctionFailedAsync(result.AuctionId, "Người về nhì không hoàn tất thanh toán trong thời hạn quy định. Phiên đấu giá kết thúc thất bại.", cancellationToken);
    }

    private async Task HandleSellerDecisionDeadlineAsync(
        AuctionDelayJob job,
        TrustSimDbContext context,
        IAuctionRealtimeNotifier realtimeNotifier,
        ILedgerService ledgerService,
        INotificationService? notificationService,
        CancellationToken cancellationToken)
    {
        var result = await context.AuctionResults
            .Include(r => r.Auction)
                .ThenInclude(a => a!.SimNumber)
            .Include(r => r.RunnerUpParticipant)
                .ThenInclude(p => p!.Deposits)
            .Include(r => r.RunnerUpParticipant)
                .ThenInclude(p => p!.UserAccount)
                    .ThenInclude(u => u!.Wallet)
            .FirstOrDefaultAsync(r => r.AuctionId == job.AuctionId, cancellationToken);

        if (result == null || result.AuctionResultStatus != AuctionResultStatus.PENDING_SELLER_CONFIRMATION)
            return;

        var now = DateTime.UtcNow;
        result.WinnerId = null;
        result.WinType = AuctionWinType.NONE;
        result.IsSellerApproved = false;
        result.AuctionResultStatus = AuctionResultStatus.FAILED;
        result.CancelledAt = now;
        result.CancelledReason = "Người bán không phản hồi quyết định trong thời hạn quy định. Phiên đấu giá kết thúc thất bại.";

        // Giải phóng SIM về AVAILABLE
        if (result.Auction?.SimNumber != null)
        {
            result.Auction.SimNumber.SimStatus = SimStatus.AVAILABLE;
        }

        // Hoàn cọc cho Runner-Up nếu còn bị phong tỏa
        if (result.RunnerUpParticipant != null)
        {
            result.RunnerUpParticipant.Status = ParticipantStatus.LOST;
            var runnerUpDeposit = result.RunnerUpParticipant.Deposits.FirstOrDefault(d => d.DepositStatus == DepositStatus.LOCKED);
            var runnerUpWallet = result.RunnerUpParticipant.UserAccount?.Wallet;

            if (runnerUpDeposit != null && runnerUpWallet != null)
            {
                runnerUpDeposit.DepositStatus = DepositStatus.RELEASED;
                runnerUpDeposit.ReleasedAT = now;

                var relJournal = await ledgerService.PostAuctionDepositReleaseAsync(
                    runnerUpWallet.Id,
                    runnerUpDeposit.DepositAmount,
                    runnerUpDeposit.Id,
                    cancellationToken);

                var refundTx = new AuctionTransaction
                {
                    Id = Guid.NewGuid(),
                    WalletId = runnerUpWallet.Id,
                    AuctionId = result.AuctionId,
                    AuctionDeposit = runnerUpDeposit,
                    AuctionTransactionType = AuctionTransactionType.AUCTION_DEPOSIT_RELEASED,
                    TransactionStatus = TransactionStatus.COMPLETED,
                    Amount = runnerUpDeposit.DepositAmount,
                    BalanceBefore = runnerUpWallet.AvailableBalance,
                    BalanceAfter = runnerUpWallet.AvailableBalance,
                    LedgerJournalId = relJournal.Id,
                    JournalCode = relJournal.JournalCode,
                    CreatedAt = now,
                    CompletedAt = now
                };
                await context.AuctionTransactions.AddAsync(refundTx, cancellationToken);

                if (notificationService != null)
                {
                    await notificationService.SendNotificationAsync(
                        userId: result.RunnerUpParticipant.UserAccountId,
                        type: NotificationType.AUCTION_DEPOSIT_REFUNDED,
                        title: $"ℹ️ Phiên đấu giá SIM {result.Auction?.SimNumber?.PhoneNumber} đã kết thúc",
                        content: $"Người bán đã quá hạn phản hồi trao quyền mua SIM. Tiền cọc {runnerUpDeposit.DepositAmount:N0} VNĐ của bạn đã được giải phóng hoàn trả về ví khả dụng.",
                        referenceType: "Auction",
                        referenceId: result.AuctionId,
                        sendEmail: true,
                        cancellationToken: cancellationToken);
                }
            }
        }

        // Hoàn cọc cam kết bán cho Người bán khi hết hạn phản hồi quyết định bán cho Á quân
        if (result.Auction?.AuctionRule?.SellerDepositAmount > 0)
        {
            var sellerWallet = await context.Wallets.FirstOrDefaultAsync(w => w.UserAccountId == result.Auction.AuthorId, cancellationToken);
            if (sellerWallet != null)
            {
                var sellerDeposit = result.Auction.AuctionRule.SellerDepositAmount;
                var relJournal = await ledgerService.PostSellerListingDepositReleaseAsync(
                    sellerWallet.Id,
                    sellerDeposit,
                    result.AuctionId,
                    cancellationToken);

                var sellerRelTx = new AuctionTransaction
                {
                    Id = Guid.NewGuid(),
                    WalletId = sellerWallet.Id,
                    AuctionId = result.AuctionId,
                    AuctionTransactionType = AuctionTransactionType.AUCTION_DEPOSIT_RELEASED,
                    TransactionStatus = TransactionStatus.COMPLETED,
                    Amount = sellerDeposit,
                    BalanceBefore = sellerWallet.AvailableBalance - sellerDeposit,
                    BalanceAfter = sellerWallet.AvailableBalance,
                    LedgerJournalId = relJournal.Id,
                    JournalCode = relJournal.JournalCode,
                    CreatedAt = now,
                    CompletedAt = now
                };
                await context.AuctionTransactions.AddAsync(sellerRelTx, cancellationToken);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        // Gửi thông báo cho Seller
        if (notificationService != null && result.Auction != null)
        {
            await notificationService.SendNotificationAsync(
                userId: result.Auction.AuthorId,
                type: NotificationType.AUCTION_FAILED,
                title: $"⚠️ Quá hạn duyệt bán cho Á quân phiên {result.Auction.AuctionCode}",
                content: $"Bạn đã quá hạn phản hồi quyết định duyệt bán SIM {result.Auction.SimNumber?.PhoneNumber} cho Á quân. Phiên đấu giá đã kết thúc và SIM đã trở lại trạng thái khả dụng.",
                referenceType: "Auction",
                referenceId: result.AuctionId,
                sendEmail: true,
                cancellationToken: cancellationToken);
        }

        await realtimeNotifier.NotifyAuctionFailedAsync(result.AuctionId, result.CancelledReason, cancellationToken);
    }

    private async Task HandleSigningDeadlineAsync(
        AuctionDelayJob job,
        TrustSimDbContext context,
        IAuctionRealtimeNotifier realtimeNotifier,
        ILedgerService ledgerService,
        INotificationService? notificationService,
        CancellationToken cancellationToken)
    {
        Guid.TryParse(job.ExtraData, out var tradeId);
        var trade = await context.TradeTransactions
            .Include(t => t.TransferContract)
                .ThenInclude(c => c!.DigitalSignatures)
            .Include(t => t.SimNumber)
            .Include(t => t.Escrow)
            .Include(t => t.Buyer)
                .ThenInclude(b => b!.Wallet)
            .Include(t => t.Seller)
                .ThenInclude(s => s!.Wallet)
            .Include(t => t.Auction)
                .ThenInclude(a => a!.AuctionRule)
            .FirstOrDefaultAsync(t => (tradeId != Guid.Empty && t.Id == tradeId) || (tradeId == Guid.Empty && t.AuctionId == job.AuctionId), cancellationToken);

        if (trade == null || trade.TransferContract == null)
            return;

        if (trade.TransferContract.ContractStatus == ContractStatus.FULLY_SIGNED)
            return;

        var buyerSig = trade.TransferContract.DigitalSignatures.FirstOrDefault(s => s.SignatureRole == SignatureRole.BUYER);
        var sellerSig = trade.TransferContract.DigitalSignatures.FirstOrDefault(s => s.SignatureRole == SignatureRole.SELLER);

        var redis = _scopeFactory.CreateScope().ServiceProvider.GetService<IConnectionMultiplexer>();

        // Kịch bản 1: Người mua ĐÃ ký, Người bán KHÔNG ký trong thời hạn 24h -> Kích hoạt Cưỡng chế Ủy quyền
        if (buyerSig != null && sellerSig == null)
        {
            _logger.LogWarning("[AuctionDelayDispatcher] ⚖️ Hết hạn 24h: Seller {SellerId} không ký hợp đồng. Kích hoạt Cưỡng chế Ủy quyền!", trade.SellerId);

            if (redis != null)
            {
                var db = redis.GetDatabase();
                await db.StreamAddAsync("stream:contracts:sign", new NameValueEntry[]
                {
                    new("tradeId", trade.Id.ToString()),
                    new("isAutoEnforced", "true")
                });
            }

            if (notificationService != null)
            {
                await notificationService.SendNotificationAsync(
                    userId: trade.SellerId,
                    type: NotificationType.SELLER_SIGNING_TIMEOUT_AUTO_ENFORCED,
                    title: "⚠️ Quá hạn ký hợp đồng bán SIM",
                    content: $"Thời hạn 24h ký hợp đồng SIM {trade.SimNumber?.PhoneNumber} đã hết. Hệ thống đã kích hoạt niêm phong cưỡng chế đại diện. Vui lòng tiến hành sang tên đúng hạn.",
                    referenceType: "TradeTransaction",
                    referenceId: trade.Id,
                    sendEmail: true,
                    cancellationToken: cancellationToken);
            }
        }
        // Kịch bản 2: Người mua KHÔNG ký sau 24h -> Hủy giao dịch, phạt cọc Người mua
        else if (buyerSig == null)
        {
            _logger.LogWarning("[AuctionDelayDispatcher] ❌ Hết hạn 24h: Buyer {BuyerId} không ký hợp đồng. Hủy giao dịch & Phạt cọc!", trade.BuyerId);

            trade.TradeTransactionStatus = TradeTransactionStatus.Cancelled_SigningTimeout;
            trade.CancelledAt = DateTime.UtcNow;
            trade.CancelledReason = "Người mua không hoàn tất ký hợp đồng điện tử trong thời hạn quy định.";

            if (trade.TransferContract != null)
            {
                trade.TransferContract.ContractStatus = ContractStatus.DRAFT;
            }

            if (trade.SimNumber != null)
            {
                trade.SimNumber.SimStatus = SimStatus.AVAILABLE;
            }

            if (trade.Seller?.Wallet != null && trade.Auction?.AuctionRule != null)
            {
                var sellerDeposit = trade.Auction.AuctionRule.SellerDepositAmount;
                if (sellerDeposit > 0)
                {
                    var relJournal = await ledgerService.PostSellerListingDepositReleaseAsync(
                        trade.Seller.Wallet.Id,
                        sellerDeposit,
                        trade.AuctionId,
                        cancellationToken);

                    var sellerRelTx = new AuctionTransaction
                    {
                        Id = Guid.NewGuid(),
                        WalletId = trade.Seller.Wallet.Id,
                        AuctionId = trade.AuctionId,
                        AuctionTransactionType = AuctionTransactionType.AUCTION_DEPOSIT_RELEASED,
                        TransactionStatus = TransactionStatus.COMPLETED,
                        Amount = sellerDeposit,
                        BalanceBefore = trade.Seller.Wallet.AvailableBalance - sellerDeposit,
                        BalanceAfter = trade.Seller.Wallet.AvailableBalance,
                        LedgerJournalId = relJournal.Id,
                        JournalCode = relJournal.JournalCode,
                        CreatedAt = DateTime.UtcNow,
                        CompletedAt = DateTime.UtcNow
                    };
                    await context.AuctionTransactions.AddAsync(sellerRelTx, cancellationToken);
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task HandleTransferDeadlineAsync(
        AuctionDelayJob job,
        TrustSimDbContext context,
        IAuctionRealtimeNotifier realtimeNotifier,
        ILedgerService ledgerService,
        INotificationService? notificationService,
        CancellationToken cancellationToken)
    {
        Guid.TryParse(job.ExtraData, out var tradeId);
        var trade = await context.TradeTransactions
            .Include(t => t.TransferContract)
            .Include(t => t.SimTransfer)
                .ThenInclude(st => st!.TransferProofs)
            .Include(t => t.SimNumber)
            .Include(t => t.Escrow)
            .Include(t => t.Buyer)
                .ThenInclude(b => b!.Wallet)
            .Include(t => t.Seller)
                .ThenInclude(s => s!.Wallet)
            .Include(t => t.Auction)
                .ThenInclude(a => a!.AuctionRule)
            .FirstOrDefaultAsync(t => (tradeId != Guid.Empty && t.Id == tradeId) || (tradeId == Guid.Empty && t.AuctionId == job.AuctionId), cancellationToken);

        if (trade == null || trade.TradeTransactionStatus != TradeTransactionStatus.PendingTransfer)
            return;

        var hasProof = trade.SimTransfer?.TransferProofs.Any() ?? false;
        var now = DateTime.UtcNow;

        if (!hasProof)
        {
            _logger.LogWarning("[AuctionDelayDispatcher] 🚨 Hết hạn chuyển nhượng SIM {TradeCode}: Người bán {SellerId} không bàn giao SIM. Hủy giao dịch & Phạt cọc!",
                trade.TradeTransactionCode, trade.SellerId);

            trade.TradeTransactionStatus = TradeTransactionStatus.Cancelled_TransferTimeout;
            trade.CancelledAt = now;
            trade.CancelledReason = "Người bán không thực hiện nghĩa vụ bàn giao và sang tên số thuê bao trong thời hạn quy định.";

            if (trade.SimTransfer != null)
            {
                trade.SimTransfer.SimTransferStatus = SimTransferStatus.CANCELLED;
                trade.SimTransfer.CancelledReason = trade.CancelledReason;
            }

            if (trade.SimNumber != null)
            {
                trade.SimNumber.SimStatus = SimStatus.AVAILABLE;
            }

            // 1. Hoàn 100% tiền Quỹ Escrow về cho Người Mua
            if (trade.Escrow != null && trade.Escrow.EscrowStatus == EscrowStatus.HELD && trade.Buyer?.Wallet != null)
            {
                var escrowRefundJournal = await ledgerService.PostEscrowRefundAsync(
                    trade.Buyer.Wallet.Id,
                    trade.Escrow.EscrowAmount,
                    trade.Escrow.Id,
                    cancellationToken);
                trade.Escrow.EscrowStatus = EscrowStatus.REFUNDED;

                var escrowRefundTx = new AuctionTransaction
                {
                    Id = Guid.NewGuid(),
                    WalletId = trade.Buyer.Wallet.Id,
                    AuctionId = trade.AuctionId,
                    EscrowId = trade.Escrow.Id,
                    AuctionTransactionType = AuctionTransactionType.ESCROW_REFUNDED,
                    TransactionStatus = TransactionStatus.COMPLETED,
                    Amount = trade.Escrow.EscrowAmount,
                    BalanceBefore = trade.Buyer.Wallet.AvailableBalance - trade.Escrow.EscrowAmount,
                    BalanceAfter = trade.Buyer.Wallet.AvailableBalance,
                    LedgerJournalId = escrowRefundJournal.Id,
                    JournalCode = escrowRefundJournal.JournalCode,
                    CreatedAt = now,
                    CompletedAt = now
                };
                await context.AuctionTransactions.AddAsync(escrowRefundTx, cancellationToken);
            }

            // 2. Tịch thu cọc Người bán (bồi thường 50% cho Người Mua và 50% cho Sàn)
            var sellerDeposit = trade.Auction?.AuctionRule?.SellerDepositAmount ?? 0m;
            if (sellerDeposit > 0 && trade.Seller?.Wallet != null && trade.Buyer?.Wallet != null)
            {
                var forfeitJournal = await ledgerService.PostSellerListingDepositForfeitedWithCompensationAsync(
                    trade.Seller.Wallet.Id,
                    trade.Buyer.Wallet.Id,
                    sellerDeposit,
                    trade.Id,
                    cancellationToken);

                var compensationAmount = Math.Round(sellerDeposit * 0.5m, 2);

                var sellerForfeitTx = new AuctionTransaction
                {
                    Id = Guid.NewGuid(),
                    WalletId = trade.Seller.Wallet.Id,
                    AuctionId = trade.AuctionId,
                    AuctionTransactionType = AuctionTransactionType.AUCTION_DEPOSIT_FORFEITED,
                    TransactionStatus = TransactionStatus.COMPLETED,
                    Amount = sellerDeposit,
                    BalanceBefore = trade.Seller.Wallet.AvailableBalance,
                    BalanceAfter = trade.Seller.Wallet.AvailableBalance,
                    LedgerJournalId = forfeitJournal.Id,
                    JournalCode = forfeitJournal.JournalCode,
                    CreatedAt = now,
                    CompletedAt = now
                };
                await context.AuctionTransactions.AddAsync(sellerForfeitTx, cancellationToken);

                if (compensationAmount > 0)
                {
                    var buyerCompTx = new AuctionTransaction
                    {
                        Id = Guid.NewGuid(),
                        WalletId = trade.Buyer.Wallet.Id,
                        AuctionId = trade.AuctionId,
                        AuctionTransactionType = AuctionTransactionType.AUCTION_COMPENSATION,
                        TransactionStatus = TransactionStatus.COMPLETED,
                        Amount = compensationAmount,
                        BalanceBefore = trade.Buyer.Wallet.AvailableBalance - compensationAmount,
                        BalanceAfter = trade.Buyer.Wallet.AvailableBalance,
                        LedgerJournalId = forfeitJournal.Id,
                        JournalCode = forfeitJournal.JournalCode,
                        CreatedAt = now,
                        CompletedAt = now
                    };
                    await context.AuctionTransactions.AddAsync(buyerCompTx, cancellationToken);
                }
            }

            await context.SaveChangesAsync(cancellationToken);

            // 3. Thông báo cho Người Mua và Người Bán
            if (notificationService != null)
            {
                await notificationService.SendNotificationAsync(
                    userId: trade.BuyerId,
                    type: NotificationType.SYSTEM_ALERT,
                    title: "⚠️ Giao dịch chuyển nhượng SIM đã bị hủy do Người bán quá hạn",
                    content: $"Người bán đã quá thời hạn bàn giao SIM {trade.SimNumber?.PhoneNumber}. Hệ thống đã hoàn trả 100% tiền Escrow và bồi thường 50% tiền cọc của Người bán vào ví của bạn.",
                    referenceType: "TradeTransaction",
                    referenceId: trade.Id,
                    sendEmail: true,
                    cancellationToken: cancellationToken);

                await notificationService.SendNotificationAsync(
                    userId: trade.SellerId,
                    type: NotificationType.SYSTEM_ALERT,
                    title: "🚨 Vi phạm thời hạn bàn giao SIM",
                    content: $"Bạn đã quá hạn thực hiện thủ tục sang tên SIM {trade.SimNumber?.PhoneNumber}. Giao dịch đã bị hủy và bạn bị xử lý chế tài tịch thu tiền cọc theo Quy chế Sàn TrustSim.",
                    referenceType: "TradeTransaction",
                    referenceId: trade.Id,
                    sendEmail: true,
                    cancellationToken: cancellationToken);
            }
        }
    }

    private async Task HandleLiquidationDeadlineAsync(
        AuctionDelayJob job,
        TrustSimDbContext context,
        IAuctionRealtimeNotifier realtimeNotifier,
        ILedgerService ledgerService,
        INotificationService? notificationService,
        CancellationToken cancellationToken)
    {
        Guid.TryParse(job.ExtraData, out var tradeId);
        var trade = await context.TradeTransactions
            .Include(t => t.TransferContract)
            .Include(t => t.SimTransfer)
                .ThenInclude(st => st!.TransferProofs)
            .Include(t => t.SimNumber)
            .Include(t => t.Escrow)
            .Include(t => t.Buyer)
                .ThenInclude(b => b!.Wallet)
            .Include(t => t.Seller)
                .ThenInclude(s => s!.Wallet)
            .Include(t => t.Auction)
                .ThenInclude(a => a!.AuctionRule)
            .FirstOrDefaultAsync(t => (tradeId != Guid.Empty && t.Id == tradeId) || (tradeId == Guid.Empty && t.AuctionId == job.AuctionId), cancellationToken);

        if (trade == null || trade.TradeTransactionStatus != TradeTransactionStatus.PendingTransfer)
            return;

        var hasProof = trade.SimTransfer?.TransferProofs.Any(p => p.ProofStatus == ProofStatus.PENDING || p.ProofStatus == ProofStatus.APPROVED) ?? false;
        if (!hasProof)
            return;

        _logger.LogInformation("[AuctionDelayDispatcher] ⏱️ Hết hạn nghiệm thu im lặng cho giao dịch {TradeCode}. Kích hoạt Tự Động Nghiệm Thu & Giải Ngân!", trade.TradeTransactionCode);

        var now = DateTime.UtcNow;

        if (trade.SimTransfer != null)
        {
            trade.SimTransfer.SimTransferStatus = SimTransferStatus.COMPLETED;
            foreach (var p in trade.SimTransfer.TransferProofs)
            {
                p.ProofStatus = ProofStatus.APPROVED;
                p.VerifiedAt = now;
            }
        }

        trade.TradeTransactionStatus = TradeTransactionStatus.Completed;
        trade.CompletedAt = now;

        var oldOwnership = await context.SimOwnerships
            .FirstOrDefaultAsync(o => o.SimNumberId == trade.SimNumberId && o.OwnershipStatus == OwnershipStatus.ACTIVE, cancellationToken);
        if (oldOwnership != null)
        {
            oldOwnership.OwnershipStatus = OwnershipStatus.ENDED;
        }

        var newBuyerOwnership = new SimOwnership
        {
            Id = Guid.NewGuid(),
            SimNumberId = trade.SimNumberId,
            UserAccountId = trade.BuyerId,
            OwnershipStatus = OwnershipStatus.ACTIVE,
            AcquistionType = AcquistionType.AUCTION_TRANSFER
        };
        await context.SimOwnerships.AddAsync(newBuyerOwnership, cancellationToken);

        var escrowAmount = trade.Escrow?.EscrowAmount ?? 0m;
        var platformFeeAmount = Math.Round(escrowAmount * 0.05m, 2);
        var netSellerPayout = escrowAmount - platformFeeAmount;

        if (trade.Escrow != null && trade.Escrow.EscrowStatus == EscrowStatus.HELD && trade.Seller?.Wallet != null && escrowAmount > 0)
        {
            var journal = await ledgerService.PostEscrowReleaseAsync(
                trade.Seller.Wallet.Id,
                escrowAmount,
                platformFeeAmount,
                trade.Escrow.Id,
                cancellationToken);
            trade.Escrow.EscrowStatus = EscrowStatus.RELEASED;
            trade.Escrow.ReleasedAt = now;

            var payoutTx = new AuctionTransaction
            {
                Id = Guid.NewGuid(),
                WalletId = trade.Seller.Wallet.Id,
                AuctionId = trade.AuctionId,
                EscrowId = trade.Escrow.Id,
                AuctionTransactionType = AuctionTransactionType.SELLER_PAYOUT,
                TransactionStatus = TransactionStatus.COMPLETED,
                Amount = netSellerPayout,
                BalanceBefore = trade.Seller.Wallet.AvailableBalance - netSellerPayout,
                BalanceAfter = trade.Seller.Wallet.AvailableBalance,
                LedgerJournalId = journal.Id,
                JournalCode = journal.JournalCode,
                CreatedAt = now,
                CompletedAt = now
            };
            await context.AuctionTransactions.AddAsync(payoutTx, cancellationToken);

            var sellerDeposit = trade.Auction?.AuctionRule?.SellerDepositAmount ?? 0m;
            if (sellerDeposit > 0 && trade.Seller.Wallet.LockedBalance >= sellerDeposit)
            {
                var releaseJournal = await ledgerService.PostSellerListingDepositReleaseAsync(
                    trade.Seller.Wallet.Id,
                    sellerDeposit,
                    trade.AuctionId,
                    cancellationToken);

                var releaseTx = new AuctionTransaction
                {
                    Id = Guid.NewGuid(),
                    WalletId = trade.Seller.Wallet.Id,
                    AuctionId = trade.AuctionId,
                    AuctionTransactionType = AuctionTransactionType.AUCTION_DEPOSIT_RELEASED,
                    TransactionStatus = TransactionStatus.COMPLETED,
                    Amount = sellerDeposit,
                    BalanceBefore = trade.Seller.Wallet.AvailableBalance - sellerDeposit,
                    BalanceAfter = trade.Seller.Wallet.AvailableBalance,
                    LedgerJournalId = releaseJournal.Id,
                    JournalCode = releaseJournal.JournalCode,
                    CreatedAt = now,
                    CompletedAt = now
                };
                await context.AuctionTransactions.AddAsync(releaseTx, cancellationToken);
            }
        }

        if (trade.TransferContract != null)
        {
            trade.TransferContract.LiquidationSealedAt = now;
            trade.TransferContract.ContractStatus = ContractStatus.COMPLETED;
        }

        await context.SaveChangesAsync(cancellationToken);

        if (notificationService != null)
        {
            await notificationService.SendNotificationAsync(
                userId: trade.SellerId,
                type: NotificationType.WALLET_TOPUP_SUCCESS,
                title: "Giao dịch bán SIM tự động hoàn tất - Tiền đã về ví!",
                content: $"Người mua đã quá thời hạn phản hồi. Hệ thống tự động nghiệm thu và giải ngân {netSellerPayout:N0} VNĐ về ví khả dụng của bạn cho SIM {trade.SimNumber?.PhoneNumber}.",
                actionUrl: $"/trade/{trade.Id}",
                referenceType: "TradeTransaction",
                referenceId: trade.Id,
                sendEmail: true,
                cancellationToken: cancellationToken);

            await notificationService.SendNotificationAsync(
                userId: trade.BuyerId,
                type: NotificationType.SYSTEM_ALERT,
                title: "Giao dịch chuyển nhượng SIM đã tự động hoàn tất",
                content: $"Hệ thống đã tự động nghiệm thu giao dịch SIM {trade.SimNumber?.PhoneNumber} do hết thời hạn phản hồi. Bạn đã là chủ sở hữu chính thức của SIM này.",
                actionUrl: $"/trade/{trade.Id}",
                referenceType: "TradeTransaction",
                referenceId: trade.Id,
                sendEmail: true,
                cancellationToken: cancellationToken);
        }
    }
}
