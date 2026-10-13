using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Helpers;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;
using TrustSimBackend.Application.Features.Auction.Hubs;
using TrustSimBackend.Application.Features.Auction.Services;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Commands;

public class ForfeitWinnerPurchaseCommandHandler : IRequestHandler<ForfeitWinnerPurchaseCommand, AuctionResultDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerService _ledgerService;
    private readonly INotificationService _notificationService;
    private readonly IAuctionRealtimeNotifier _realtimeNotifier;
    private readonly IAuctionDelayQueueService _delayQueue;

    public ForfeitWinnerPurchaseCommandHandler(
        IUnitOfWork unitOfWork,
        ILedgerService ledgerService,
        INotificationService notificationService,
        IAuctionRealtimeNotifier realtimeNotifier,
        IAuctionDelayQueueService delayQueue)
    {
        _unitOfWork = unitOfWork;
        _ledgerService = ledgerService;
        _notificationService = notificationService;
        _realtimeNotifier = realtimeNotifier;
        _delayQueue = delayQueue;
    }

    public async Task<AuctionResultDto> Handle(ForfeitWinnerPurchaseCommand request, CancellationToken cancellationToken)
    {
        var auction = await _unitOfWork.Auctions.GetByIdAsync(request.AuctionId, cancellationToken);
        if (auction == null)
            throw new NotFoundException($"Không tìm thấy phiên đấu giá với ID '{request.AuctionId}'.");

        var participant = await _unitOfWork.Auctions.GetParticipantAsync(request.AuctionId, request.UserId, cancellationToken);
        if (participant == null || participant.Status != ParticipantStatus.WON)
            throw new ForbiddenAccessException("Bạn không phải là Người thắng cuộc trong phiên đấu giá này.");

        var auctionResult = await _unitOfWork.Auctions.GetResultByAuctionIdAsync(auction.Id, cancellationToken);
        if (auctionResult == null || auctionResult.AuctionResultStatus != AuctionResultStatus.PENDING_PAYMENT)
            throw new BadRequestException("Phiên đấu giá không ở trạng thái chờ người thắng thanh toán (PENDING_PAYMENT).");

        var winnerWallet = await _unitOfWork.Wallets.GetByUserIdAsync(request.UserId, cancellationToken);
        if (winnerWallet == null)
            throw new NotFoundException("Không tìm thấy ví tiền của người dùng.");

        var sellerWallet = await _unitOfWork.Wallets.GetByUserIdAsync(auction.AuthorId, cancellationToken);
        if (sellerWallet == null)
            throw new NotFoundException("Không tìm thấy ví tiền của Người bán để bồi thường.");

        var now = DateTime.UtcNow;

        // 1. Hủy job hẹn giờ Payment Deadline trong Delay Queue
        await _delayQueue.CancelJobAsync(auction.Id, AuctionDelayJobTypes.PAYMENT_DEADLINE, cancellationToken);

        // 2. Tịch thu cọc và bồi thường 50% Seller / 50% Sàn qua Sổ cái kép
        participant.Status = ParticipantStatus.DEFAULTED;
        _unitOfWork.Auctions.UpdateParticipant(participant);

        var lockedDeposits = participant.Deposits.Where(d => d.DepositStatus == DepositStatus.LOCKED).ToList();
        decimal totalDepositAmount = lockedDeposits.Sum(d => d.DepositAmount);
        decimal compensationAmount = 0m;

        if (lockedDeposits.Count > 0)
        {
            compensationAmount = Math.Round(totalDepositAmount * 0.50m, 2);
            auctionResult.SellerCompensationAmount = compensationAmount;
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
                dep.ForfeitedReason = string.IsNullOrWhiteSpace(request.Reason)
                    ? "Người thắng chủ động từ bỏ quyền mua SIM sau đấu giá."
                    : request.Reason;

                var forfeitureTx = new AuctionTransaction
                {
                    Id = Guid.NewGuid(),
                    WalletId = winnerWallet.Id,
                    AuctionId = auction.Id,
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
                await _unitOfWork.AuctionTransactions.AddAsync(forfeitureTx, cancellationToken);
            }
        }

        // Bắn thông báo phạt cọc cho Winner trong mọi trường hợp bùng kèo
        await _notificationService.SendNotificationAsync(
            userId: request.UserId,
            type: NotificationType.AUCTION_DEPOSIT_FORFEITED,
            title: $"⚠️ Tịch thu tiền cọc phiên {auction.AuctionCode}",
            content: $"Bạn đã từ bỏ quyền mua SIM {auction.SimNumber?.PhoneNumber}. Tổng tiền cọc {totalDepositAmount:N0} VNĐ đã bị tịch thu theo quy chế.",
            referenceType: "Auction",
            referenceId: auction.Id,
            sendEmail: true,
            cancellationToken: cancellationToken);

        // 3. Cập nhật Bid của Winner cũ sang REJECTED
        var bids = await _unitOfWork.Bids.GetBidsByAuctionIdAsync(auction.Id, top: 200, cancellationToken);
        var winnerBid = bids.FirstOrDefault(b => b.ParticipantId == participant.Id && b.Status == BidStatus.WINNING);
        if (winnerBid != null)
        {
            winnerBid.Status = BidStatus.REJECTED;
            _unitOfWork.Bids.Update(winnerBid);
        }

        // 4. Bộ lọc liêm chính 3 tầng GapRate & Độc lập
        var rule = auction.AuctionRule;
        var minUniqueBidders = rule?.MinimumUniqueBiddersForRunnerUp ?? 2;
        var maxGapRatio = rule?.MaxPriceGapRatioForRunnerUp ?? 1.50m;
        var requireSellerApproval = rule?.RequireSellerApprovalForRunnerUp ?? true;

        var uniqueBiddersCount = bids.Select(b => b.ParticipantId).Distinct().Count();
        auctionResult.UniqueBiddersCount = uniqueBiddersCount;

        bool isShieldingDetected = false;
        string failureReason = string.Empty;

        if (auctionResult.RunnerUpParticipantId.HasValue && auctionResult.RunnerUpFinalPrice.HasValue && auctionResult.RunnerUpFinalPrice.Value > 0)
        {
            var gapRatio = auctionResult.FinalPrice / auctionResult.RunnerUpFinalPrice.Value;
            auctionResult.PriceGapRatio = Math.Round(gapRatio, 2);

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
            failureReason = "Phiên đấu giá không có Người về nhì hợp lệ.";
        }

        // 5. Cây quyết định & Phát Thông báo
        if (isShieldingDetected || !auctionResult.RunnerUpParticipantId.HasValue || !auctionResult.RunnerUpFinalPrice.HasValue)
        {
            auctionResult.WinnerId = null;
            auctionResult.WinType = AuctionWinType.NONE;
            auctionResult.AuctionResultStatus = isShieldingDetected ? AuctionResultStatus.INVALIDATED_SHIELDING : AuctionResultStatus.FAILED;
            auctionResult.CancelledAt = now;
            auctionResult.CancelledReason = isShieldingDetected ? failureReason : "Winner bùng kèo và không có Runner-Up.";
            _unitOfWork.Auctions.UpdateResult(auctionResult);

            var sim = await _unitOfWork.SimNumbers.GetByIdAsync(auction.SimNumberId, cancellationToken);
            if (sim != null)
            {
                sim.SimStatus = SimStatus.AVAILABLE;
                _unitOfWork.SimNumbers.Update(sim);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Bắn thông báo cho Seller kèm số tiền bồi thường
            if (compensationAmount > 0)
            {
                await _notificationService.SendNotificationAsync(
                    userId: auction.AuthorId,
                    type: NotificationType.AUCTION_FAILED,
                    title: $"💰 Nhận đền bù {compensationAmount:N0}đ phiên {auction.AuctionCode}",
                    content: $"Người thắng đã từ bỏ quyền mua SIM {auction.SimNumber?.PhoneNumber}. Bạn đã nhận {compensationAmount:N0} VNĐ (50% tiền cọc) vào ví. Phiên đấu giá kết thúc.",
                    referenceType: "Auction",
                    referenceId: auction.Id,
                    sendEmail: true,
                    cancellationToken: cancellationToken);
            }

            await _realtimeNotifier.NotifyAuctionFailedAsync(auction.Id, auctionResult.CancelledReason, cancellationToken);

            return new AuctionResultDto
            {
                Id = auctionResult.Id,
                AuctionId = auction.Id,
                WinnerId = null,
                FinalPrice = auctionResult.FinalPrice,
                InitialWinnerId = auctionResult.InitialWinnerId,
                InitialWinningPrice = auctionResult.InitialWinningPrice,
                RunnerUpParticipantId = auctionResult.RunnerUpParticipantId,
                RunnerUpFinalPrice = auctionResult.RunnerUpFinalPrice,
                WinType = AuctionWinType.NONE,
                AuctionResultStatus = auctionResult.AuctionResultStatus,
                CancelledReason = auctionResult.CancelledReason
            };
        }

        // Nhánh hỏi ý kiến Seller
        if (requireSellerApproval)
        {
            var graceMinutes = rule?.PaymentGracePeriodMinutes > 0
                ? rule.PaymentGracePeriodMinutes
                : (rule?.PaymentGracePeriodHours > 0 ? rule.PaymentGracePeriodHours * 60 : 1440);
            var sellerDeadline = now.AddMinutes(graceMinutes);

            auctionResult.WinnerId = null;
            auctionResult.WinType = AuctionWinType.NONE;
            auctionResult.AuctionResultStatus = AuctionResultStatus.PENDING_SELLER_CONFIRMATION;
            auctionResult.SellerDecisionDeadline = sellerDeadline;
            _unitOfWork.Auctions.UpdateResult(auctionResult);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Lập lịch Job timeout quyết định của Seller vào Redis Delay Queue
            await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
            {
                AuctionId = auction.Id,
                JobType = AuctionDelayJobTypes.SELLER_DECISION_DEADLINE,
                ExecuteAt = sellerDeadline
            }, cancellationToken);

            // Gửi thông báo cho Seller mời xác nhận kèm số tiền đền bù
            await _notificationService.SendNotificationAsync(
                userId: auction.AuthorId,
                type: NotificationType.AUCTION_SELLER_CONFIRMATION_REQUIRED,
                title: $"💰 Nhận đền bù {compensationAmount:N0}đ & Mời bán cho Á quân",
                content: $"Winner phiên {auction.AuctionCode} đã từ bỏ quyền mua. Bạn đã nhận {compensationAmount:N0} VNĐ vào ví. Á quân trả giá {auctionResult.RunnerUpFinalPrice:N0} VNĐ. Bạn có đồng ý bán cho Á quân không?",
                actionUrl: $"/seller/auctions/{auction.Id}/confirm-runner-up",
                referenceType: "Auction",
                referenceId: auction.Id,
                metadata: new { compensation = compensationAmount, runnerUpPrice = auctionResult.RunnerUpFinalPrice, deadline = sellerDeadline },
                sendEmail: true,
                cancellationToken: cancellationToken);

            await _realtimeNotifier.NotifyAuctionStatusChangedAsync(
                auction.Id,
                AuctionResultStatus.PENDING_SELLER_CONFIRMATION.ToString(),
                auctionResult.RunnerUpParticipantId,
                auctionResult.RunnerUpFinalPrice,
                cancellationToken);

            return new AuctionResultDto
            {
                Id = auctionResult.Id,
                AuctionId = auction.Id,
                WinnerId = null,
                FinalPrice = auctionResult.FinalPrice,
                InitialWinnerId = auctionResult.InitialWinnerId,
                InitialWinningPrice = auctionResult.InitialWinningPrice,
                RunnerUpParticipantId = auctionResult.RunnerUpParticipantId,
                RunnerUpFinalPrice = auctionResult.RunnerUpFinalPrice,
                WinType = AuctionWinType.NONE,
                AuctionResultStatus = AuctionResultStatus.PENDING_SELLER_CONFIRMATION
            };
        }

        // Nhánh tự động chuyển cho Runner-Up
        var autoGraceMinutes = rule?.PaymentGracePeriodMinutes > 0
            ? rule.PaymentGracePeriodMinutes
            : (rule?.PaymentGracePeriodHours > 0 ? rule.PaymentGracePeriodHours * 60 : 1440);
        var runnerUpDeadline = now.AddMinutes(autoGraceMinutes);
        auctionResult.WinnerId = null;
        auctionResult.WinType = AuctionWinType.NONE;
        auctionResult.AuctionResultStatus = AuctionResultStatus.RUNNER_UP_PENDING;
        auctionResult.IsRunnerUpOffered = true;
        auctionResult.RunnerUpPaymentDeadline = runnerUpDeadline;
        _unitOfWork.Auctions.UpdateResult(auctionResult);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
        {
            AuctionId = auction.Id,
            JobType = AuctionDelayJobTypes.RUNNER_UP_DEADLINE,
            ExecuteAt = runnerUpDeadline
        }, cancellationToken);

        // Bắn thông báo mời Runner-Up thanh toán
        var runnerUpParticipant = await _unitOfWork.Auctions.GetParticipantByIdAsync(auctionResult.RunnerUpParticipantId.Value, cancellationToken);
        if (runnerUpParticipant != null)
        {
            await _notificationService.SendNotificationAsync(
                userId: runnerUpParticipant.UserAccountId,
                type: NotificationType.AUCTION_RUNNER_UP_OFFERED,
                title: $"🎉 Chúc mừng bạn được trao quyền mua SIM {auction.SimNumber?.PhoneNumber}!",
                content: $"Người thắng phiên {auction.AuctionCode} đã từ bỏ quyền mua. Bạn là Á quân với mức giá {auctionResult.RunnerUpFinalPrice:N0} VNĐ. Vui lòng hoàn tất thanh toán trước {runnerUpDeadline.ToVietnamTimeString("HH:mm dd/MM/yyyy")}.",
                actionUrl: $"/auctions/{auction.Id}/payment",
                referenceType: "Auction",
                referenceId: auction.Id,
                sendEmail: true,
                cancellationToken: cancellationToken);
        }

        // Bắn thông báo bồi thường cho Seller ở nhánh tự động chuyển Á quân
        if (compensationAmount > 0)
        {
            await _notificationService.SendNotificationAsync(
                userId: auction.AuthorId,
                type: NotificationType.SYSTEM_ALERT,
                title: $"💰 Nhận đền bù {compensationAmount:N0}đ & Chuyển quyền mua cho Á quân",
                content: $"Winner phiên {auction.AuctionCode} đã từ bỏ quyền mua. Bạn đã nhận {compensationAmount:N0} VNĐ (50% tiền cọc) vào ví. Hệ thống đang tự động trao quyền mua cho Á quân với giá {auctionResult.RunnerUpFinalPrice:N0} VNĐ.",
                referenceType: "Auction",
                referenceId: auction.Id,
                sendEmail: true,
                cancellationToken: cancellationToken);
        }

        await _realtimeNotifier.NotifyAuctionStatusChangedAsync(
            auction.Id,
            AuctionResultStatus.RUNNER_UP_PENDING.ToString(),
            auctionResult.RunnerUpParticipantId,
            auctionResult.RunnerUpFinalPrice,
            cancellationToken);

        return new AuctionResultDto
        {
            Id = auctionResult.Id,
            AuctionId = auction.Id,
            WinnerId = null,
            FinalPrice = auctionResult.FinalPrice,
            InitialWinnerId = auctionResult.InitialWinnerId,
            InitialWinningPrice = auctionResult.InitialWinningPrice,
            RunnerUpParticipantId = auctionResult.RunnerUpParticipantId,
            RunnerUpFinalPrice = auctionResult.RunnerUpFinalPrice,
            WinType = AuctionWinType.NONE,
            AuctionResultStatus = AuctionResultStatus.RUNNER_UP_PENDING,
            IsRunnerUpOffered = true,
            RunnerUpPaymentDeadline = runnerUpDeadline
        };
    }
}
