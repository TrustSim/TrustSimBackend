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

/// <summary>
/// Handler xử lý Người bán (Seller) duyệt bán hoặc từ chối bán SIM cho Á quân (Runner-Up)
/// khi Người thắng bùng kèo và phiên đã vượt qua Bộ lọc liêm chính 3 tầng.
/// </summary>
public class SellerApproveRunnerUpCommandHandler : IRequestHandler<SellerApproveRunnerUpCommand, AuctionResultDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerService _ledgerService;
    private readonly INotificationService _notificationService;
    private readonly IAuctionRealtimeNotifier _realtimeNotifier;
    private readonly IAuctionDelayQueueService _delayQueue;

    public SellerApproveRunnerUpCommandHandler(
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

    public async Task<AuctionResultDto> Handle(SellerApproveRunnerUpCommand request, CancellationToken cancellationToken)
    {
        var auction = await _unitOfWork.Auctions.GetByIdAsync(request.AuctionId, cancellationToken);
        if (auction == null)
            throw new NotFoundException($"Không tìm thấy phiên đấu giá với ID '{request.AuctionId}'.");

        if (auction.AuthorId != request.SellerUserId)
            throw new ForbiddenAccessException("Chỉ Người bán tạo phiên đấu giá này mới có quyền quyết định.");

        var auctionResult = await _unitOfWork.Auctions.GetResultByAuctionIdAsync(auction.Id, cancellationToken);
        if (auctionResult == null || auctionResult.AuctionResultStatus != AuctionResultStatus.PENDING_SELLER_CONFIRMATION)
            throw new BadRequestException("Phiên đấu giá không ở trạng thái chờ Người bán xác nhận (PENDING_SELLER_CONFIRMATION).");

        if (!auctionResult.RunnerUpParticipantId.HasValue || !auctionResult.RunnerUpFinalPrice.HasValue)
            throw new BadRequestException("Phiên đấu giá không có Người về nhì hợp lệ.");

        var runnerUpParticipant = await _unitOfWork.Auctions.GetParticipantByIdAsync(auctionResult.RunnerUpParticipantId.Value, cancellationToken);
        if (runnerUpParticipant == null)
            throw new NotFoundException("Không tìm thấy thông tin Người về nhì.");

        var now = DateTime.UtcNow;

        // Hủy Job timeout duyệt của Seller trong Redis Delay Queue
        await _delayQueue.CancelJobAsync(auction.Id, AuctionDelayJobTypes.SELLER_DECISION_DEADLINE, cancellationToken);

        // TRƯỜNG HỢP 1: SELLER ĐỒNG Ý BÁN CHO Á QUÂN
        if (request.IsApproved)
        {
            var graceMinutes = auction.AuctionRule?.PaymentGracePeriodMinutes > 0
                ? auction.AuctionRule.PaymentGracePeriodMinutes
                : (auction.AuctionRule?.PaymentGracePeriodHours > 0 ? auction.AuctionRule.PaymentGracePeriodHours * 60 : 1440);
            var runnerUpDeadline = now.AddMinutes(graceMinutes);

            auctionResult.IsSellerApproved = true;
            auctionResult.AuctionResultStatus = AuctionResultStatus.RUNNER_UP_PENDING;
            auctionResult.IsRunnerUpOffered = true;
            auctionResult.RunnerUpPaymentDeadline = runnerUpDeadline;
            _unitOfWork.Auctions.UpdateResult(auctionResult);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Lập lịch hạn thanh toán Runner-Up vào Delay Queue
            await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
            {
                AuctionId = auction.Id,
                JobType = AuctionDelayJobTypes.RUNNER_UP_DEADLINE,
                ExecuteAt = runnerUpDeadline
            }, cancellationToken);

            // Gửi thông báo mời Runner-Up thanh toán
            await _notificationService.SendNotificationAsync(
                userId: runnerUpParticipant.UserAccountId,
                type: NotificationType.AUCTION_RUNNER_UP_OFFERED,
                title: $"🎉 Người bán đã duyệt bán SIM {auction.SimNumber?.PhoneNumber} cho bạn!",
                content: $"Người bán đã chấp thuận trao quyền mua SIM {auction.SimNumber?.PhoneNumber} với giá đặt của bạn là {auctionResult.RunnerUpFinalPrice:N0} VNĐ. Vui lòng thanh toán trước {runnerUpDeadline.ToVietnamTimeString("HH:mm dd/MM/yyyy")}.",
                actionUrl: $"/auctions/{auction.Id}/payment",
                referenceType: "Auction",
                referenceId: auction.Id,
                sendEmail: true,
                cancellationToken: cancellationToken);

            // Gửi thông báo xác nhận cho Seller
            await _notificationService.SendNotificationAsync(
                userId: auction.AuthorId,
                type: NotificationType.SYSTEM_ALERT,
                title: $"✅ Đã duyệt bán SIM {auction.SimNumber?.PhoneNumber} cho Á quân",
                content: $"Bạn đã đồng ý bán SIM cho Á quân với mức giá {auctionResult.RunnerUpFinalPrice:N0} VNĐ. Hệ thống đang chờ Á quân thanh toán.",
                referenceType: "Auction",
                referenceId: auction.Id,
                cancellationToken: cancellationToken);

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
                WinnerId = auctionResult.WinnerId,
                FinalPrice = auctionResult.FinalPrice,
                RunnerUpParticipantId = auctionResult.RunnerUpParticipantId,
                RunnerUpFinalPrice = auctionResult.RunnerUpFinalPrice,
                AuctionResultStatus = AuctionResultStatus.RUNNER_UP_PENDING,
                IsRunnerUpOffered = true,
                RunnerUpPaymentDeadline = runnerUpDeadline
            };
        }

        // TRƯỜNG HỢP 2: SELLER TỪ CHỐI BÁN CHO Á QUÂN
        auctionResult.WinnerId = null;
        auctionResult.WinType = AuctionWinType.NONE;
        auctionResult.IsSellerApproved = false;
        auctionResult.AuctionResultStatus = AuctionResultStatus.FAILED;
        auctionResult.CancelledAt = now;
        auctionResult.CancelledReason = string.IsNullOrWhiteSpace(request.RejectionReason)
            ? "Người bán đã từ chối bán cho Á quân."
            : $"Người bán từ chối bán cho Á quân. Lý do: {request.RejectionReason}";
        _unitOfWork.Auctions.UpdateResult(auctionResult);

        // Trả SIM về AVAILABLE
        var sim = await _unitOfWork.SimNumbers.GetByIdAsync(auction.SimNumberId, cancellationToken);
        if (sim != null)
        {
            sim.SimStatus = SimStatus.AVAILABLE;
            _unitOfWork.SimNumbers.Update(sim);
        }

        // Hoàn cọc cho Runner-Up nếu còn bị phong tỏa
        var runnerUpDeposit = runnerUpParticipant.Deposits.FirstOrDefault(d => d.DepositStatus == DepositStatus.LOCKED);
        var runnerUpWallet = await _unitOfWork.Wallets.GetByUserIdAsync(runnerUpParticipant.UserAccountId, cancellationToken);
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
                AuctionId = auction.Id,
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
            await _unitOfWork.AuctionTransactions.AddAsync(refundTx, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Gửi thông báo cho Runner-Up
        await _notificationService.SendNotificationAsync(
            userId: runnerUpParticipant.UserAccountId,
            type: NotificationType.AUCTION_DEPOSIT_REFUNDED,
            title: $"ℹ️ Phiên đấu giá SIM {auction.SimNumber?.PhoneNumber} đã kết thúc",
            content: $"Người bán đã từ chối chuyển nhượng SIM cho mức giá Á quân. Tiền cọc {runnerUpDeposit?.DepositAmount:N0} VNĐ của bạn đã được giải phóng hoàn trả về ví khả dụng.",
            referenceType: "Auction",
            referenceId: auction.Id,
            sendEmail: true,
            cancellationToken: cancellationToken);

        // Gửi thông báo xác nhận cho Seller
        await _notificationService.SendNotificationAsync(
            userId: auction.AuthorId,
            type: NotificationType.AUCTION_FAILED,
            title: $"ℹ️ Đã từ chối bán SIM {auction.SimNumber?.PhoneNumber} cho Á quân",
            content: $"Bạn đã từ chối bán SIM cho Á quân phiên {auction.AuctionCode}. Phiên đấu giá kết thúc và SIM đã trở lại trạng thái Sẵn sàng.",
            referenceType: "Auction",
            referenceId: auction.Id,
            cancellationToken: cancellationToken);

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
            AuctionResultStatus = AuctionResultStatus.FAILED,
            CancelledReason = auctionResult.CancelledReason
        };
    }
}
