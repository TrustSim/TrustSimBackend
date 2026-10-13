using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;
using TrustSimBackend.Application.Features.Auction.Hubs;
using TrustSimBackend.Application.Features.Auction.Services;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Commands;

public class RejectRunnerUpOfferCommandHandler : IRequestHandler<RejectRunnerUpOfferCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerService _ledgerService;
    private readonly INotificationService _notificationService;
    private readonly IAuctionRealtimeNotifier _realtimeNotifier;
    private readonly IAuctionDelayQueueService _delayQueue;

    public RejectRunnerUpOfferCommandHandler(
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

    public async Task<bool> Handle(RejectRunnerUpOfferCommand request, CancellationToken cancellationToken)
    {
        var auction = await _unitOfWork.Auctions.GetByIdAsync(request.AuctionId, cancellationToken);
        if (auction == null)
        {
            throw new NotFoundException($"Không tìm thấy phiên đấu giá với ID '{request.AuctionId}'.");
        }

        var participant = await _unitOfWork.Auctions.GetParticipantAsync(request.AuctionId, request.UserId, cancellationToken);
        if (participant == null || participant.Status != ParticipantStatus.RUNNER_UP)
        {
            throw new ForbiddenAccessException("Bạn không phải là Người về nhì trong phiên đấu giá này.");
        }

        // Hủy deadline trong Delay Queue
        await _delayQueue.CancelJobAsync(auction.Id, AuctionDelayJobTypes.RUNNER_UP_DEADLINE, cancellationToken);

        var now = DateTime.UtcNow;

        // Trả SIM về AVAILABLE
        var sim = await _unitOfWork.SimNumbers.GetByIdAsync(auction.SimNumberId, cancellationToken);
        if (sim != null)
        {
            sim.SimStatus = SimStatus.AVAILABLE;
            _unitOfWork.SimNumbers.Update(sim);
        }

        // Hoàn cọc cho Runner-Up nếu còn giữ
        var runnerUpDeposit = participant.Deposits.FirstOrDefault(d => d.DepositStatus == DepositStatus.LOCKED);
        var runnerUpWallet = await _unitOfWork.Wallets.GetByUserIdAsync(participant.UserAccountId, cancellationToken);
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

        var auctionResult = await _unitOfWork.Auctions.GetResultByAuctionIdAsync(auction.Id, cancellationToken);
        if (auctionResult != null)
        {
            auctionResult.WinnerId = null;
            auctionResult.WinType = AuctionWinType.NONE;
            auctionResult.AuctionResultStatus = AuctionResultStatus.FAILED;
            auctionResult.CancelledAt = now;
            auctionResult.CancelledReason = "Người về nhì đã từ chối nhận quyền mua SIM.";
            _unitOfWork.Auctions.UpdateResult(auctionResult);
        }

        participant.Status = ParticipantStatus.LOST;
        _unitOfWork.Auctions.UpdateParticipant(participant);

        // Hoàn cọc cam kết bán cho Người bán khi Á quân từ chối mua SIM
        if (auction.AuctionRule?.SellerDepositAmount > 0)
        {
            var sellerWallet = await _unitOfWork.Wallets.GetByUserIdAsync(auction.AuthorId, cancellationToken);
            if (sellerWallet != null)
            {
                var sellerDeposit = auction.AuctionRule.SellerDepositAmount;
                await _ledgerService.PostSellerListingDepositReleaseAsync(
                    sellerWallet.Id,
                    sellerDeposit,
                    auction.Id,
                    cancellationToken);
                sellerWallet.AvailableBalance += sellerDeposit;
                sellerWallet.LockedBalance -= sellerDeposit;
                _unitOfWork.Wallets.Update(sellerWallet);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Gửi thông báo cho Seller và Runner-Up
        await _notificationService.SendNotificationAsync(
            userId: auction.AuthorId,
            type: NotificationType.AUCTION_FAILED,
            title: $"ℹ️ Á quân từ chối mua SIM {sim?.PhoneNumber}",
            content: $"Người về nhì đã từ chối nhận quyền mua SIM phiên {auction.AuctionCode}. Phiên đấu giá kết thúc không thành công và SIM đã được chuyển về trạng thái Sẵn sàng.",
            referenceType: "Auction",
            referenceId: auction.Id,
            sendEmail: true,
            cancellationToken: cancellationToken);

        await _notificationService.SendNotificationAsync(
            userId: participant.UserAccountId,
            type: NotificationType.AUCTION_DEPOSIT_REFUNDED,
            title: $"ℹ️ Đã từ chối quyền mua SIM {sim?.PhoneNumber}",
            content: $"Bạn đã từ chối mua SIM phiên {auction.AuctionCode}. Tiền cọc {runnerUpDeposit?.DepositAmount:N0} VNĐ của bạn đã được giải phóng hoàn trả đầy đủ.",
            referenceType: "Auction",
            referenceId: auction.Id,
            cancellationToken: cancellationToken);

        await _realtimeNotifier.NotifyAuctionFailedAsync(
            auction.Id,
            "Người về nhì đã từ chối nhận quyền mua SIM. Phiên đấu giá kết thúc thất bại.",
            cancellationToken);

        return true;
    }
}
