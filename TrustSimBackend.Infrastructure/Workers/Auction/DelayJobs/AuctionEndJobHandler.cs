using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Helpers;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;
using TrustSimBackend.Application.Features.Auction.Hubs;
using TrustSimBackend.Application.Features.Auction.Services;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Infrastructure.Persistence;
using AuctionEntity = TrustSimBackend.Domain.Entities.Auctions.Auction;

namespace TrustSimBackend.Infrastructure.Workers.Auction.DelayJobs;

/// <summary>
/// Xử lý kết thúc phiên đấu giá kiểu Anh (AUCTION_END), quyết toán cọc/Escrow, bắn SignalR và tạo thông báo hậu kỳ.
/// </summary>
public sealed class AuctionEndJobHandler : IAuctionDelayJobHandler
{
    private readonly TrustSimDbContext _context;
    private readonly IAuctionRealtimeNotifier _realtimeNotifier;
    private readonly ILedgerService _ledgerService;
    private readonly IAuctionDelayQueueService _delayQueue;
    private readonly SellerListingDepositHelper _depositHelper;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuctionEndJobHandler> _logger;
    private readonly IRedisAuctionCacheService? _redisCache;
    private readonly INotificationService? _notificationService;

    public AuctionEndJobHandler(
        TrustSimDbContext context,
        IAuctionRealtimeNotifier realtimeNotifier,
        ILedgerService ledgerService,
        IAuctionDelayQueueService delayQueue,
        SellerListingDepositHelper depositHelper,
        IServiceScopeFactory scopeFactory,
        ILogger<AuctionEndJobHandler> logger,
        IRedisAuctionCacheService? redisCache = null,
        INotificationService? notificationService = null)
    {
        _context = context;
        _realtimeNotifier = realtimeNotifier;
        _ledgerService = ledgerService;
        _delayQueue = delayQueue;
        _depositHelper = depositHelper;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _redisCache = redisCache;
        _notificationService = notificationService;
    }

    public bool CanHandle(string jobType) => jobType == AuctionDelayJobTypes.AUCTION_END;

    public async Task HandleAsync(AuctionDelayJob job, CancellationToken cancellationToken)
    {
        await WaitForPendingRedisBidFlushAsync(job.AuctionId, cancellationToken);

        var auction = await _context.Auctions
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
        {
            return;
        }

        var now = DateTime.UtcNow;
        auction.AuctionStatus = AuctionStatus.END;
        auction.IsActive = false;
        auction.LockVersion++;

        if (_redisCache != null)
        {
            await _redisCache.EvictAuctionCacheWithTtlAsync(auction.Id, TimeSpan.FromHours(24), cancellationToken);
        }

        var acceptedBids = auction.Bids
            .Where(b => b.Status == BidStatus.ACCEPTED || b.Status == BidStatus.WINNING || b.Status == BidStatus.OUTBIB)
            .OrderByDescending(b => b.BidAmount)
            .ToList();

        var topBid = acceptedBids.FirstOrDefault();
        if (topBid == null)
        {
            await HandleNoBidsEndAsync(auction, now, cancellationToken);
            return;
        }

        var runnerUpBid = acceptedBids.Skip(1).FirstOrDefault();
        var runnerUpParticipant = runnerUpBid != null
            ? auction.Participants.FirstOrDefault(p => p.Id == runnerUpBid.ParticipantId)
            : null;
        var winnerParticipant = auction.Participants.FirstOrDefault(p => p.Id == topBid.ParticipantId);
        topBid.Status = BidStatus.WINNING;

        var graceMinutes = SellerListingDepositHelper.ResolvePaymentGraceMinutes(auction.AuctionRule);
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
        await _context.AuctionResults.AddAsync(resultEntity, cancellationToken);

        await RefundLoserDepositsAsync(auction, winnerParticipant, runnerUpParticipant, now, cancellationToken);

        TradeTransaction? settledTrade = null;
        if (winnerParticipant?.UserAccount?.Wallet != null)
        {
            var winWallet = winnerParticipant.UserAccount.Wallet;
            var winnerDeposit = winnerParticipant.Deposits.FirstOrDefault(d => d.DepositStatus == DepositStatus.LOCKED);
            var depositPaid = winnerDeposit?.DepositAmount ?? 0m;
            var remainingRequired = topBid.BidAmount - depositPaid;

            if (winWallet.AvailableBalance >= remainingRequired)
            {
                settledTrade = await SettleWinnerImmediatelyAsync(
                    auction,
                    winnerParticipant,
                    winWallet,
                    winnerDeposit,
                    topBid.BidAmount,
                    depositPaid,
                    remainingRequired,
                    resultEntity,
                    now,
                    cancellationToken);
            }
            else
            {
                await CollectProgressiveMarginAsync(
                    auction,
                    winnerParticipant,
                    winWallet,
                    topBid.BidAmount,
                    depositPaid,
                    resultEntity,
                    paymentDeadline,
                    now,
                    cancellationToken);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        if (settledTrade != null)
        {
            await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
            {
                AuctionId = auction.Id,
                JobType = AuctionDelayJobTypes.SIGNING_DEADLINE,
                ExecuteAt = settledTrade.SigningDeadline!.Value,
                ExtraData = settledTrade.Id.ToString()
            }, cancellationToken);
        }
        else if (winnerParticipant != null)
        {
            await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
            {
                AuctionId = auction.Id,
                JobType = AuctionDelayJobTypes.PAYMENT_DEADLINE,
                ExecuteAt = paymentDeadline
            }, cancellationToken);
        }

        var resultDto = new AuctionResultDto
        {
            Id = resultEntity.Id,
            AuctionId = resultEntity.AuctionId,
            WinnerId = resultEntity.WinnerId,
            WinnerUserName = winnerParticipant?.UserAccount?.UserName,
            FinalPrice = resultEntity.FinalPrice,
            InitialWinnerId = resultEntity.InitialWinnerId,
            InitialWinningPrice = resultEntity.InitialWinningPrice,
            RunnerUpParticipantId = resultEntity.RunnerUpParticipantId,
            RunnerUpFinalPrice = resultEntity.RunnerUpFinalPrice,
            WinType = resultEntity.WinType,
            AuctionResultStatus = resultEntity.AuctionResultStatus,
            PaymentDeadline = resultEntity.PaymentDeadline,
            SettledAt = resultEntity.SettledAt,
            DeterminedAt = resultEntity.DeterminedAt,
            ConfirmedAt = resultEntity.ConfirmedAt
        };

        await _realtimeNotifier.NotifyAuctionStatusChangedAsync(
            auction.Id,
            AuctionStatus.END.ToString(),
            resultEntity.WinnerId,
            resultEntity.FinalPrice,
            cancellationToken);

        await _realtimeNotifier.NotifyAuctionEndedAsync(auction.Id, resultDto, cancellationToken);
        _logger.LogInformation("[LiveAuctionWorker] 🏁 Phiên '{Code}' đã kết thúc. Người thắng: {WinnerId}, Giá: {Price:N0} VNĐ",
            auction.AuctionCode, resultEntity.WinnerId, resultEntity.FinalPrice);

        await SendPostEndNotificationsAsync(auction, winnerParticipant, topBid, settledTrade, paymentDeadline, now, cancellationToken);
    }

    /// <summary>
    /// Chờ ngắn tối đa ~800ms nếu lượt bid dẫn đầu trên Redis chưa kịp được PersistenceWorker xả xuống DB,
    /// đảm bảo không bỏ sót lượt bid ở 500ms cuối trước khi khóa sổ.
    /// </summary>
    private async Task WaitForPendingRedisBidFlushAsync(Guid auctionId, CancellationToken cancellationToken)
    {
        if (_redisCache == null) return;

        var topRedisBids = await _redisCache.GetTopBiddersAsync(auctionId, 1, cancellationToken);
        if (topRedisBids == null || topRedisBids.Count == 0)
        {
            return;
        }

        var topBidId = topRedisBids[0].BidId;
        const int maxAttempts = 8;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var existsInDb = await _context.Bids
                .AsNoTracking()
                .AnyAsync(b => b.Id == topBidId, cancellationToken);

            if (existsInDb)
            {
                return;
            }

            await Task.Delay(100, cancellationToken);
        }

        _logger.LogWarning(
            "[LiveAuctionWorker] Hết thời gian chờ PersistenceWorker xả bid cuối {BidId} cho phiên {AuctionId}, tiếp tục chốt theo dữ liệu DB hiện có.",
            topBidId, auctionId);
    }

    private async Task HandleNoBidsEndAsync(AuctionEntity auction, DateTime now, CancellationToken cancellationToken)
    {
        if (auction.SimNumber != null)
        {
            auction.SimNumber.SimStatus = SimStatus.AVAILABLE;
        }

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
        await _context.AuctionResults.AddAsync(noWinnerResult, cancellationToken);

        foreach (var p in auction.Participants)
        {
            p.Status = ParticipantStatus.REFUNDED;
            var w = p.UserAccount?.Wallet;
            var dep = p.Deposits.FirstOrDefault(d => d.DepositStatus == DepositStatus.LOCKED);

            if (w != null && dep != null)
            {
                dep.DepositStatus = DepositStatus.RELEASED;
                dep.ReleasedAT = now;

                var relJournal = await _ledgerService.PostAuctionDepositReleaseAsync(w.Id, dep.DepositAmount, dep.Id, cancellationToken);
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
                await _context.AuctionTransactions.AddAsync(refundTx, cancellationToken);

                if (_notificationService != null)
                {
                    await _notificationService.SendNotificationAsync(
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

        await _realtimeNotifier.NotifyAuctionStatusChangedAsync(
            auction.Id,
            AuctionStatus.END.ToString(),
            null,
            auction.StartingPrice,
            cancellationToken);

        await _realtimeNotifier.NotifyAuctionFailedAsync(auction.Id, "Phiên đấu giá kết thúc không có người tham gia đặt giá.", cancellationToken);

        if (_notificationService != null)
        {
            await _notificationService.SendNotificationAsync(
                userId: auction.AuthorId,
                type: NotificationType.AUCTION_FAILED,
                title: $"ℹ️ Phiên đấu giá SIM {auction.SimNumber?.PhoneNumber} kết thúc thất bại",
                content: $"Phiên đấu giá {auction.AuctionCode} đã kết thúc nhưng không có lượt đặt giá nào. SIM đã được chuyển về trạng thái Sẵn sàng.",
                referenceType: "Auction",
                referenceId: auction.Id,
                sendEmail: true,
                cancellationToken: cancellationToken);
        }
    }

    private async Task RefundLoserDepositsAsync(
        AuctionEntity auction,
        AuctionParticipant? winnerParticipant,
        AuctionParticipant? runnerUpParticipant,
        DateTime now,
        CancellationToken cancellationToken)
    {
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

                var relJournal = await _ledgerService.PostAuctionDepositReleaseAsync(wallet.Id, dep.DepositAmount, dep.Id, cancellationToken);
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
                await _context.AuctionTransactions.AddAsync(refundTx, cancellationToken);

                if (_notificationService != null)
                {
                    await _notificationService.SendNotificationAsync(
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
    }

    private async Task<TradeTransaction> SettleWinnerImmediatelyAsync(
        AuctionEntity auction,
        AuctionParticipant winnerParticipant,
        Wallet winWallet,
        AuctionDeposit? winnerDeposit,
        decimal bidAmount,
        decimal depositPaid,
        decimal remainingRequired,
        AuctionResult resultEntity,
        DateTime now,
        CancellationToken cancellationToken)
    {
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
            EscrowAmount = bidAmount,
            EscrowStatus = EscrowStatus.HELD,
            AuctionDepositId = winnerDeposit?.Id,
            HeldAt = now
        };
        trade.Escrow = escrow;

        var journal = await _ledgerService.PostEscrowHoldAsync(winWallet.Id, bidAmount, depositPaid, escrowId, cancellationToken);

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
            Amount = bidAmount,
            BalanceBefore = winWallet.AvailableBalance + remainingRequired,
            BalanceAfter = winWallet.AvailableBalance,
            LedgerJournalId = journal.Id,
            JournalCode = journal.JournalCode,
            CreatedAt = now,
            CompletedAt = now
        };
        await _context.AuctionTransactions.AddAsync(auctionTx, cancellationToken);

        if (auction.SimNumber != null)
        {
            auction.SimNumber.SimStatus = SimStatus.TRANSFERRING;
        }

        winWallet.WalletStatus = WalletStatus.ACTIVE;
        resultEntity.AuctionResultStatus = AuctionResultStatus.CONFIRMED;
        resultEntity.WinType = AuctionWinType.ORIGINAL_WINNER;
        resultEntity.SettledAt = now;

        await _context.Escrows.AddAsync(escrow, cancellationToken);
        await _context.TradeTransactions.AddAsync(trade, cancellationToken);
        return trade;
    }

    private async Task CollectProgressiveMarginAsync(
        AuctionEntity auction,
        AuctionParticipant winnerParticipant,
        Wallet winWallet,
        decimal bidAmount,
        decimal depositPaid,
        AuctionResult resultEntity,
        DateTime paymentDeadline,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var marginRate = auction.AuctionRule?.MarginPercentage ?? 0.10m;
        var requiredMarginTotal = Math.Round(bidAmount * marginRate, 2);
        var deltaMargin = Math.Max(0, requiredMarginTotal - depositPaid);

        if (deltaMargin > 0 && winWallet.AvailableBalance > 0)
        {
            var actualHoldAmount = Math.Min(winWallet.AvailableBalance, deltaMargin);
            var addDepositId = Guid.NewGuid();
            var depCode = $"DEP-ADD-{now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";

            var holdJournal = await _ledgerService.PostAuctionProgressiveDepositLockAsync(
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

            await _context.AuctionDeposits.AddAsync(progressiveDeposit, cancellationToken);
            await _context.AuctionTransactions.AddAsync(lockTx, cancellationToken);

            _logger.LogInformation(
                "[LiveAuctionWorker] 🔒 Đã thu cọc bổ sung lũy tiến {Amount:N0} VNĐ từ Winner {User} cho phiên {Auction}",
                actualHoldAmount, winnerParticipant.UserAccount?.UserName, auction.AuctionCode);
        }

        winWallet.WalletStatus = WalletStatus.ACTIVE;
        resultEntity.AuctionResultStatus = AuctionResultStatus.PENDING_PAYMENT;
        resultEntity.WinType = AuctionWinType.NONE;
        resultEntity.PaymentDeadline = paymentDeadline;
    }

    private async Task SendPostEndNotificationsAsync(
        AuctionEntity auction,
        AuctionParticipant? winnerParticipant,
        Bid topBid,
        TradeTransaction? settledTrade,
        DateTime paymentDeadline,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var winnerName = winnerParticipant?.UserAccount?.UserName ?? "Người tham gia";

        if (settledTrade != null && winnerParticipant != null)
        {
            string? winningNoticeUrl = null;
            try
            {
                using var pdfScope = _scopeFactory.CreateScope();
                var pdfService = pdfScope.ServiceProvider.GetService<IPdfContractService>();
                if (pdfService != null)
                {
                    winningNoticeUrl = await pdfService.EnsureWinningNoticeUploadedAsync(settledTrade.Id, cancellationToken);
                    _logger.LogInformation("[LiveAuctionWorker] 📄 Đã tạo và lưu Phiếu chốt giá trúng trên Supabase CDN: {Url}", winningNoticeUrl);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[LiveAuctionWorker] Lỗi khi tạo Phiếu chốt giá trúng cho Trade {TradeCode}", settledTrade.TradeTransactionCode);
            }

            var graceHours = auction.AuctionRule?.SigningGracePeriodHours ?? 24;

            if (_notificationService != null)
            {
                await _notificationService.SendNotificationAsync(
                    userId: winnerParticipant.UserAccountId,
                    type: NotificationType.CONTRACT_SIGNING_REQUIRED,
                    title: $"📝 Yêu cầu ký hợp đồng SIM {auction.SimNumber?.PhoneNumber}!",
                    content: $"Chúc mừng bạn đã trúng đấu giá SIM {auction.SimNumber?.PhoneNumber} với mức giá {topBid.BidAmount:N0} VNĐ (Đã thanh toán Escrow). Vui lòng xem biên bản chốt giá trúng và ký hợp đồng điện tử trong vòng {graceHours} giờ.",
                    actionUrl: winningNoticeUrl ?? $"/trades/{settledTrade.Id}",
                    referenceType: "TradeTransaction",
                    referenceId: settledTrade.Id,
                    sendEmail: true,
                    cancellationToken: cancellationToken);

                await _notificationService.SendNotificationAsync(
                    userId: auction.AuthorId,
                    type: NotificationType.CONTRACT_SIGNING_REQUIRED,
                    title: $"📝 Người thắng {winnerName} đã thanh toán! Vui lòng ký hợp đồng",
                    content: $"Phiên {auction.AuctionCode} đã kết thúc. Người thắng ({winnerName}) đã thanh toán đủ {topBid.BidAmount:N0} VNĐ vào Escrow. Vui lòng xem biên bản chốt giá trúng và ký hợp đồng điện tử trong vòng {graceHours} giờ.",
                    actionUrl: winningNoticeUrl ?? $"/trades/{settledTrade.Id}",
                    referenceType: "TradeTransaction",
                    referenceId: settledTrade.Id,
                    sendEmail: true,
                    cancellationToken: cancellationToken);
            }
        }
        else if (winnerParticipant != null && _notificationService != null)
        {
            await _notificationService.SendNotificationAsync(
                userId: winnerParticipant.UserAccountId,
                type: NotificationType.AUCTION_WON,
                title: $"🏆 Bạn đã thắng đấu giá SIM {auction.SimNumber?.PhoneNumber}!",
                content: $"Mức giá trúng của bạn là {topBid.BidAmount:N0} VNĐ. Vui lòng nạp thêm và hoàn tất thanh toán trước {paymentDeadline.ToVietnamTimeString("HH:mm dd/MM/yyyy")}.",
                actionUrl: $"/auctions/{auction.Id}/payment",
                referenceType: "Auction",
                referenceId: auction.Id,
                sendEmail: true,
                cancellationToken: cancellationToken);

            await _notificationService.SendNotificationAsync(
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
