using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Helpers;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Auction.Hubs;
using TrustSimBackend.Application.Features.Auction.Services;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Infrastructure.Persistence;

namespace TrustSimBackend.Infrastructure.Workers.Auction.DelayJobs;

/// <summary>
/// Xử lý các mốc quá hạn thanh toán sau phiên: PAYMENT_DEADLINE, RUNNER_UP_DEADLINE, SELLER_DECISION_DEADLINE.
/// </summary>
public sealed class SettlementDeadlineJobHandler : IAuctionDelayJobHandler
{
    private readonly TrustSimDbContext _context;
    private readonly IAuctionRealtimeNotifier _realtimeNotifier;
    private readonly ILedgerService _ledgerService;
    private readonly IAuctionDelayQueueService _delayQueue;
    private readonly SellerListingDepositHelper _depositHelper;
    private readonly ILogger<SettlementDeadlineJobHandler> _logger;
    private readonly INotificationService? _notificationService;

    public SettlementDeadlineJobHandler(
        TrustSimDbContext context,
        IAuctionRealtimeNotifier realtimeNotifier,
        ILedgerService ledgerService,
        IAuctionDelayQueueService delayQueue,
        SellerListingDepositHelper depositHelper,
        ILogger<SettlementDeadlineJobHandler> logger,
        INotificationService? notificationService = null)
    {
        _context = context;
        _realtimeNotifier = realtimeNotifier;
        _ledgerService = ledgerService;
        _delayQueue = delayQueue;
        _depositHelper = depositHelper;
        _logger = logger;
        _notificationService = notificationService;
    }

    public bool CanHandle(string jobType)
        => jobType == AuctionDelayJobTypes.PAYMENT_DEADLINE
        || jobType == AuctionDelayJobTypes.RUNNER_UP_DEADLINE
        || jobType == AuctionDelayJobTypes.SELLER_DECISION_DEADLINE;

    public Task HandleAsync(AuctionDelayJob job, CancellationToken cancellationToken) => job.JobType switch
    {
        AuctionDelayJobTypes.PAYMENT_DEADLINE => HandlePaymentDeadlineAsync(job.AuctionId, cancellationToken),
        AuctionDelayJobTypes.RUNNER_UP_DEADLINE => HandleRunnerUpDeadlineAsync(job.AuctionId, cancellationToken),
        AuctionDelayJobTypes.SELLER_DECISION_DEADLINE => HandleSellerDecisionDeadlineAsync(job.AuctionId, cancellationToken),
        _ => Task.CompletedTask
    };

    private async Task HandlePaymentDeadlineAsync(Guid auctionId, CancellationToken cancellationToken)
    {
        var result = await _context.AuctionResults
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
            .FirstOrDefaultAsync(r => r.AuctionId == auctionId, cancellationToken);

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

        if (result.Winner != null)
        {
            result.Winner.Status = ParticipantStatus.DEFAULTED;
            var winnerWallet = result.Winner.UserAccount?.Wallet;
            var sellerWallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserAccountId == auction.AuthorId, cancellationToken);

            if (lockedDeposits.Count > 0 && winnerWallet != null && sellerWallet != null)
            {
                compensationAmount = Math.Round(totalDepositAmount * 0.50m, 2);
                result.SellerCompensationAmount = compensationAmount;
                var primaryDepositId = lockedDeposits.First().Id;

                var forfeitJournal = await _ledgerService.PostAuctionDepositForfeitedWithCompensationAsync(
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
                    await _context.AuctionTransactions.AddAsync(forfeitureTx, cancellationToken);
                }

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
                    await _context.AuctionTransactions.AddAsync(sellerCompTx, cancellationToken);
                }

                _logger.LogInformation(
                    "[PostAuctionWorker] ⚠️ Tịch thu toàn bộ cọc {Total:N0} VNĐ ({Count} khoản cọc) của Winner {User}. Đền bù Seller {Comp:N0} VNĐ",
                    totalDepositAmount, lockedDeposits.Count, result.Winner?.UserAccount?.UserName, compensationAmount);
            }

            var winningBid = await _context.Bids.FirstOrDefaultAsync(
                b => b.AuctionId == result.AuctionId && b.ParticipantId == result.WinnerId && b.Status == BidStatus.WINNING,
                cancellationToken);
            if (winningBid != null)
            {
                winningBid.Status = BidStatus.REJECTED;
            }

            if (_notificationService != null)
            {
                await _notificationService.SendNotificationAsync(
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

        var rule = auction.AuctionRule;
        var minUniqueBidders = rule?.MinimumUniqueBiddersForRunnerUp ?? 2;
        var maxGapRatio = rule?.MaxPriceGapRatioForRunnerUp ?? 1.50m;
        var requireSellerApproval = rule?.RequireSellerApprovalForRunnerUp ?? true;

        var bids = await _context.Bids.Where(b => b.AuctionId == auction.Id).ToListAsync(cancellationToken);
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

            if (_notificationService != null && compensationAmount > 0)
            {
                await _notificationService.SendNotificationAsync(
                    userId: auction.AuthorId,
                    type: NotificationType.AUCTION_FAILED,
                    title: $"💰 Nhận đền bù {compensationAmount:N0}đ phiên {auction.AuctionCode}",
                    content: $"Người thắng phiên {auction.AuctionCode} đã quá hạn thanh toán. Bạn đã nhận {compensationAmount:N0} VNĐ (50% tiền cọc) vào ví. Phiên đấu giá kết thúc không thành công.",
                    referenceType: "Auction",
                    referenceId: auction.Id,
                    sendEmail: true,
                    cancellationToken: cancellationToken);
            }

            await _realtimeNotifier.NotifyAuctionFailedAsync(result.AuctionId, result.CancelledReason, cancellationToken);
            return;
        }

        var graceMinutes = SellerListingDepositHelper.ResolvePaymentGraceMinutes(rule);

        if (requireSellerApproval)
        {
            var sellerDeadline = now.AddMinutes(graceMinutes);

            result.WinnerId = null;
            result.WinType = AuctionWinType.NONE;
            result.AuctionResultStatus = AuctionResultStatus.PENDING_SELLER_CONFIRMATION;
            result.SellerDecisionDeadline = sellerDeadline;
            await _context.SaveChangesAsync(cancellationToken);

            await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
            {
                AuctionId = result.AuctionId,
                JobType = AuctionDelayJobTypes.SELLER_DECISION_DEADLINE,
                ExecuteAt = sellerDeadline
            }, cancellationToken);

            if (_notificationService != null)
            {
                await _notificationService.SendNotificationAsync(
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

            await _realtimeNotifier.NotifyAuctionStatusChangedAsync(
                result.AuctionId,
                AuctionResultStatus.PENDING_SELLER_CONFIRMATION.ToString(),
                result.RunnerUpParticipantId,
                result.RunnerUpFinalPrice,
                cancellationToken);

            return;
        }

        var runnerUpDeadlineAuto = now.AddMinutes(graceMinutes);

        result.WinnerId = null;
        result.WinType = AuctionWinType.NONE;
        result.AuctionResultStatus = AuctionResultStatus.RUNNER_UP_PENDING;
        result.IsRunnerUpOffered = true;
        result.RunnerUpPaymentDeadline = runnerUpDeadlineAuto;
        await _context.SaveChangesAsync(cancellationToken);

        await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
        {
            AuctionId = result.AuctionId,
            JobType = AuctionDelayJobTypes.RUNNER_UP_DEADLINE,
            ExecuteAt = runnerUpDeadlineAuto
        }, cancellationToken);

        if (_notificationService != null)
        {
            if (result.RunnerUpParticipant != null)
            {
                await _notificationService.SendNotificationAsync(
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
                await _notificationService.SendNotificationAsync(
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

        await _realtimeNotifier.NotifyAuctionStatusChangedAsync(
            result.AuctionId,
            AuctionResultStatus.RUNNER_UP_PENDING.ToString(),
            result.RunnerUpParticipantId,
            result.RunnerUpFinalPrice,
            cancellationToken);
    }

    private async Task HandleRunnerUpDeadlineAsync(Guid auctionId, CancellationToken cancellationToken)
    {
        var result = await _context.AuctionResults
            .Include(r => r.Auction)
                .ThenInclude(a => a!.SimNumber)
            .Include(r => r.RunnerUpParticipant)
            .FirstOrDefaultAsync(r => r.AuctionId == auctionId, cancellationToken);

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

        await _context.SaveChangesAsync(cancellationToken);

        if (_notificationService != null)
        {
            if (result.RunnerUpParticipant != null)
            {
                await _notificationService.SendNotificationAsync(
                    userId: result.RunnerUpParticipant.UserAccountId,
                    type: NotificationType.AUCTION_FAILED,
                    title: $"⚠️ Quá hạn thanh toán quyền mua SIM {result.Auction?.SimNumber?.PhoneNumber}",
                    content: "Bạn đã quá hạn thanh toán theo mức giá Á quân. Quyền mua SIM đã bị hủy.",
                    referenceType: "Auction",
                    referenceId: result.AuctionId,
                    sendEmail: true,
                    cancellationToken: cancellationToken);
            }

            if (result.Auction != null)
            {
                await _notificationService.SendNotificationAsync(
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

        await _realtimeNotifier.NotifyAuctionFailedAsync(result.AuctionId, "Người về nhì không hoàn tất thanh toán trong thời hạn quy định. Phiên đấu giá kết thúc thất bại.", cancellationToken);
    }

    private async Task HandleSellerDecisionDeadlineAsync(Guid auctionId, CancellationToken cancellationToken)
    {
        var result = await _context.AuctionResults
            .Include(r => r.Auction)
                .ThenInclude(a => a!.SimNumber)
            .Include(r => r.Auction)
                .ThenInclude(a => a!.AuctionRule)
            .Include(r => r.RunnerUpParticipant)
                .ThenInclude(p => p!.Deposits)
            .Include(r => r.RunnerUpParticipant)
                .ThenInclude(p => p!.UserAccount)
                    .ThenInclude(u => u!.Wallet)
            .FirstOrDefaultAsync(r => r.AuctionId == auctionId, cancellationToken);

        if (result == null || result.AuctionResultStatus != AuctionResultStatus.PENDING_SELLER_CONFIRMATION)
            return;

        var now = DateTime.UtcNow;
        result.WinnerId = null;
        result.WinType = AuctionWinType.NONE;
        result.IsSellerApproved = false;
        result.AuctionResultStatus = AuctionResultStatus.FAILED;
        result.CancelledAt = now;
        result.CancelledReason = "Người bán không phản hồi quyết định trong thời hạn quy định. Phiên đấu giá kết thúc thất bại.";

        if (result.Auction?.SimNumber != null)
        {
            result.Auction.SimNumber.SimStatus = SimStatus.AVAILABLE;
        }

        if (result.RunnerUpParticipant != null)
        {
            result.RunnerUpParticipant.Status = ParticipantStatus.LOST;
            var runnerUpDeposit = result.RunnerUpParticipant.Deposits.FirstOrDefault(d => d.DepositStatus == DepositStatus.LOCKED);
            var runnerUpWallet = result.RunnerUpParticipant.UserAccount?.Wallet;

            if (runnerUpDeposit != null && runnerUpWallet != null)
            {
                runnerUpDeposit.DepositStatus = DepositStatus.RELEASED;
                runnerUpDeposit.ReleasedAT = now;

                var relJournal = await _ledgerService.PostAuctionDepositReleaseAsync(
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
                await _context.AuctionTransactions.AddAsync(refundTx, cancellationToken);

                if (_notificationService != null)
                {
                    await _notificationService.SendNotificationAsync(
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

        if (result.Auction?.AuctionRule?.SellerDepositAmount > 0)
        {
            var sellerWallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserAccountId == result.Auction.AuthorId, cancellationToken);
            await _depositHelper.TryReleaseSellerListingDepositAsync(
                _context,
                _ledgerService,
                sellerWallet,
                result.AuctionId,
                result.Auction.AuctionRule.SellerDepositAmount,
                now,
                cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        if (_notificationService != null && result.Auction != null)
        {
            await _notificationService.SendNotificationAsync(
                userId: result.Auction.AuthorId,
                type: NotificationType.AUCTION_FAILED,
                title: $"⚠️ Quá hạn duyệt bán cho Á quân phiên {result.Auction.AuctionCode}",
                content: $"Bạn đã quá hạn phản hồi quyết định duyệt bán SIM {result.Auction.SimNumber?.PhoneNumber} cho Á quân. Phiên đấu giá đã kết thúc và SIM đã trở lại trạng thái khả dụng.",
                referenceType: "Auction",
                referenceId: result.AuctionId,
                sendEmail: true,
                cancellationToken: cancellationToken);
        }

        await _realtimeNotifier.NotifyAuctionFailedAsync(result.AuctionId, result.CancelledReason, cancellationToken);
    }
}
