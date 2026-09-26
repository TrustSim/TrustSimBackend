using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;
using TrustSimBackend.Application.Features.Auction.Hubs;
using TrustSimBackend.Application.Features.Auction.Services;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;
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

        _logger.LogInformation("[AuctionDelayDispatcher] Đồng bộ Startup hoàn tất ({SchedCount} scheduled, {ActCount} active, {PayCount} pending payments).",
            scheduledAuctions.Count, activeAuctions.Count, pendingPayments.Count);
    }

    private async Task ProcessJobAsync(AuctionDelayJob job, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrustSimDbContext>();
        var realtimeNotifier = scope.ServiceProvider.GetRequiredService<IAuctionRealtimeNotifier>();
        var ledgerService = scope.ServiceProvider.GetRequiredService<ILedgerService>();

        switch (job.JobType)
        {
            case AuctionDelayJobTypes.AUCTION_START:
                await HandleAuctionStartAsync(job, context, realtimeNotifier, cancellationToken);
                break;

            case AuctionDelayJobTypes.DUTCH_PRICE_DROP:
                await HandleDutchPriceDropAsync(job, context, realtimeNotifier, ledgerService, cancellationToken);
                break;

            case AuctionDelayJobTypes.AUCTION_END:
                await HandleAuctionEndAsync(job, context, realtimeNotifier, ledgerService, cancellationToken);
                break;

            case AuctionDelayJobTypes.PAYMENT_DEADLINE:
                await HandlePaymentDeadlineAsync(job, context, realtimeNotifier, ledgerService, cancellationToken);
                break;

            case AuctionDelayJobTypes.RUNNER_UP_DEADLINE:
                await HandleRunnerUpDeadlineAsync(job, context, realtimeNotifier, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        var auction = await context.Auctions
            .Include(a => a.AuctionRule)
            .Include(a => a.Participants)
                .ThenInclude(p => p.UserAccount)
                    .ThenInclude(u => u!.Wallet)
            .FirstOrDefaultAsync(a => a.Id == job.AuctionId, cancellationToken);

        if (auction == null || auction.AuctionStatus != AuctionStatus.SCHEDULE)
            return;

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
            // Kết thúc Thất bại (NO_WINNER)
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
                AuctionResultStatus = AuctionResultStatus.NO_WINNER,
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
                        BalanceBefore = wallet.AvailableBalance - deposit.DepositAmount,
                        BalanceAfter = wallet.AvailableBalance,
                        LedgerJournalId = relJournal.Id,
                        JournalCode = relJournal.JournalCode,
                        CreatedAt = now,
                        CompletedAt = now
                    };
                    await context.AuctionTransactions.AddAsync(refundTx, cancellationToken);
                }

                if (wallet != null)
                {
                    wallet.WalletStatus = WalletStatus.ACTIVE;
                }
            }

            await context.SaveChangesAsync(cancellationToken);
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

        // Tìm Top 1 Winning Bid và Top 2 Runner-Up Bid
        var acceptedBids = auction.Bids
            .Where(b => b.Status == BidStatus.ACCEPTED || b.Status == BidStatus.WINNING || b.Status == BidStatus.OUTBIB)
            .OrderByDescending(b => b.BidAmount)
            .ToList();

        var topBid = acceptedBids.FirstOrDefault();

        if (topBid == null)
        {
            // Không có bid nào -> NO_WINNER
            if (auction.SimNumber != null)
                auction.SimNumber.SimStatus = SimStatus.AVAILABLE;

            var noWinnerResult = new AuctionResult
            {
                Id = Guid.NewGuid(),
                AuctionId = auction.Id,
                WinnerId = null,
                FinalPrice = auction.StartingPrice,
                AuctionResultStatus = AuctionResultStatus.NO_WINNER,
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
                        BalanceBefore = w.AvailableBalance - dep.DepositAmount,
                        BalanceAfter = w.AvailableBalance,
                        LedgerJournalId = relJournal.Id,
                        JournalCode = relJournal.JournalCode,
                        CreatedAt = now,
                        CompletedAt = now
                    };
                    await context.AuctionTransactions.AddAsync(refundTx, cancellationToken);
                }

                if (w != null)
                {
                    w.WalletStatus = WalletStatus.ACTIVE;
                }
            }

            await context.SaveChangesAsync(cancellationToken);
            await realtimeNotifier.NotifyAuctionFailedAsync(auction.Id, "Phiên đấu giá kết thúc không có người tham gia đặt giá.", cancellationToken);
            return;
        }

        // Có người thắng
        var winnerParticipant = auction.Participants.FirstOrDefault(p => p.Id == topBid.ParticipantId);
        topBid.Status = BidStatus.WINNING;

        // Tìm Runner-up
        var runnerUpBid = acceptedBids
            .FirstOrDefault(b => winnerParticipant == null || b.ParticipantId != winnerParticipant.Id);

        var runnerUpParticipant = runnerUpBid != null
            ? auction.Participants.FirstOrDefault(p => p.Id == runnerUpBid.ParticipantId)
            : null;

        var graceHours = auction.AuctionRule?.PaymentGracePeriodHours ?? 24;
        var paymentDeadline = now.AddHours(graceHours);

        var resultEntity = new AuctionResult
        {
            Id = Guid.NewGuid(),
            AuctionId = auction.Id,
            WinnerId = winnerParticipant?.Id,
            FinalPrice = topBid.BidAmount,
            RunnerUpParticipantId = runnerUpParticipant?.Id,
            RunnerUpFinalPrice = runnerUpBid?.BidAmount,
            AuctionResultStatus = AuctionResultStatus.PENDING_PAYMENT,
            PaymentDeadline = paymentDeadline,
            DeterminedAt = now,
            ConfirmedAt = now
        };

        // Hoàn cọc và unfreeze ví cho toàn bộ người thua (bao gồm cả Runner-up)
        foreach (var p in auction.Participants)
        {
            if (winnerParticipant != null && p.Id == winnerParticipant.Id)
            {
                p.Status = ParticipantStatus.WON;
                var w = p.UserAccount?.Wallet;
                if (w != null)
                {
                    w.WalletStatus = WalletStatus.ACTIVE; // Mở ví để nạp thêm nếu thiếu
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

                var trade = new TradeTransaction
                {
                    Id = tradeId,
                    TradeTransactionCode = $"TRD-{now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
                    TradeTransactionStatus = TradeTransactionStatus.PendingTransfer,
                    AuctionId = auction.Id,
                    BuyerId = winnerParticipant.UserAccountId,
                    SellerId = auction.AuthorId,
                    SimNumberId = auction.SimNumberId,
                    EscrowDeadline = now.AddDays(2),
                    SigningDeadline = now.AddDays(3),
                    TransferDeadline = now.AddDays(7),
                    IsActive = true
                };

                var escrow = new Escrow
                {
                    Id = escrowId,
                    TradeTransactionId = trade.Id,
                    EscrowAmount = topBid.BidAmount,
                    EscrowStatus = EscrowStatus.HELD,
                    AuctionDepositId = winnerDeposit?.Id,
                    HeldAt = now
                };

                // Ghi nhận chứng từ sổ cái kép ESCROW_HOLD (Bút toán 3 dòng nguyên tử)
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

                resultEntity.AuctionResultStatus = AuctionResultStatus.CONFIRMED;
                resultEntity.SettledAt = now;

                await context.Escrows.AddAsync(escrow, cancellationToken);
                await context.TradeTransactions.AddAsync(trade, cancellationToken);
            }
            else
            {
                // Lập lịch Payment Deadline cho Người thắng trong Redis Delay Queue
                resultEntity.AuctionResultStatus = AuctionResultStatus.PENDING_PAYMENT;
                resultEntity.PaymentDeadline = paymentDeadline;

                await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
                {
                    AuctionId = auction.Id,
                    JobType = AuctionDelayJobTypes.PAYMENT_DEADLINE,
                    ExecuteAt = paymentDeadline
                }, cancellationToken);
            }
        }

        await context.AuctionResults.AddAsync(resultEntity, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var resultDto = new AuctionResultDto
        {
            Id = resultEntity.Id,
            AuctionId = resultEntity.AuctionId,
            WinnerId = resultEntity.WinnerId,
            FinalPrice = resultEntity.FinalPrice,
            RunnerUpParticipantId = resultEntity.RunnerUpParticipantId,
            RunnerUpFinalPrice = resultEntity.RunnerUpFinalPrice,
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
            .FirstOrDefaultAsync(r => r.AuctionId == job.AuctionId, cancellationToken);

        if (result == null || result.AuctionResultStatus != AuctionResultStatus.PENDING_PAYMENT)
            return;

        var now = DateTime.UtcNow;

        // 1. Tịch thu tiền cọc của Người thắng bùng kèo qua Sổ cái kế toán kép
        if (result.Winner != null)
        {
            result.Winner.Status = ParticipantStatus.DEFAULTED;
            var deposit = result.Winner.Deposits.FirstOrDefault(d => d.DepositStatus == DepositStatus.LOCKED);
            var wallet = result.Winner.UserAccount?.Wallet;

            if (deposit != null && wallet != null)
            {
                deposit.DepositStatus = DepositStatus.FORFEITED;
                deposit.ForfeitedAt = now;
                deposit.ForfeitedReason = "Quá hạn thanh toán tiền mua SIM đấu giá.";

                var forfeitJournal = await ledgerService.PostAuctionDepositForfeitedAsync(
                    wallet.Id,
                    deposit.DepositAmount,
                    deposit.Id,
                    cancellationToken);

                var forfeitureTx = new AuctionTransaction
                {
                    Id = Guid.NewGuid(),
                    WalletId = wallet.Id,
                    AuctionId = result.AuctionId,
                    AuctionDeposit = deposit,
                    AuctionTransactionType = AuctionTransactionType.AUCTION_DEPOSIT_FORFEITED,
                    TransactionStatus = TransactionStatus.COMPLETED,
                    Amount = deposit.DepositAmount,
                    BalanceBefore = wallet.AvailableBalance,
                    BalanceAfter = wallet.AvailableBalance,
                    LedgerJournalId = forfeitJournal.Id,
                    JournalCode = forfeitJournal.JournalCode,
                    CreatedAt = now,
                    CompletedAt = now
                };
                await context.AuctionTransactions.AddAsync(forfeitureTx, cancellationToken);
            }
        }

        // 2. Chuyển quyền mua cho Người về nhì (nếu có) theo đúng giá Bid cuối của Runner-up (RunnerUpFinalPrice)
        if (result.RunnerUpParticipantId.HasValue && result.RunnerUpParticipant != null && result.RunnerUpFinalPrice.HasValue)
        {
            var graceHours = result.Auction?.AuctionRule?.PaymentGracePeriodHours ?? 24;
            var runnerUpDeadline = now.AddHours(graceHours);

            result.AuctionResultStatus = AuctionResultStatus.RUNNER_UP_PENDING;
            result.IsRunnerUpOffered = true;
            result.RunnerUpPaymentDeadline = runnerUpDeadline;

            await context.SaveChangesAsync(cancellationToken);

            // Lập lịch Runner Up Deadline
            await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
            {
                AuctionId = result.AuctionId,
                JobType = AuctionDelayJobTypes.RUNNER_UP_DEADLINE,
                ExecuteAt = runnerUpDeadline
            }, cancellationToken);

            await realtimeNotifier.NotifyAuctionStatusChangedAsync(
                result.AuctionId,
                AuctionResultStatus.RUNNER_UP_PENDING.ToString(),
                result.RunnerUpParticipantId,
                result.RunnerUpFinalPrice,
                cancellationToken);

            _logger.LogInformation("[AuctionDelayDispatcher] ⚠️ Người thắng bùng kèo. Chuyển quyền mua cho Người về nhì (ID: {RunnerUpId}) với giá bid của họ: {Price:N0} VNĐ",
                result.RunnerUpParticipantId, result.RunnerUpFinalPrice);
            return;
        }

        // Không có Người về nhì -> Phiên thất bại hoàn toàn
        result.AuctionResultStatus = AuctionResultStatus.FAILED;
        if (result.Auction?.SimNumber != null)
        {
            result.Auction.SimNumber.SimStatus = SimStatus.AVAILABLE;
        }

        await context.SaveChangesAsync(cancellationToken);
        await realtimeNotifier.NotifyAuctionFailedAsync(result.AuctionId, "Người thắng không hoàn tất thanh toán trong thời hạn quy định. Phiên đấu giá kết thúc thất bại.", cancellationToken);
    }

    private async Task HandleRunnerUpDeadlineAsync(
        AuctionDelayJob job,
        TrustSimDbContext context,
        IAuctionRealtimeNotifier realtimeNotifier,
        CancellationToken cancellationToken)
    {
        var result = await context.AuctionResults
            .Include(r => r.Auction)
                .ThenInclude(a => a!.SimNumber)
            .FirstOrDefaultAsync(r => r.AuctionId == job.AuctionId, cancellationToken);

        if (result == null || result.AuctionResultStatus != AuctionResultStatus.RUNNER_UP_PENDING)
            return;

        result.AuctionResultStatus = AuctionResultStatus.FAILED;
        if (result.Auction?.SimNumber != null)
        {
            result.Auction.SimNumber.SimStatus = SimStatus.AVAILABLE;
        }

        await context.SaveChangesAsync(cancellationToken);
        await realtimeNotifier.NotifyAuctionFailedAsync(result.AuctionId, "Người về nhì không hoàn tất thanh toán trong thời hạn quy định. Phiên đấu giá kết thúc thất bại.", cancellationToken);
    }
}
