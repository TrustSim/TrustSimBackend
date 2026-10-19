using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;
using TrustSimBackend.Application.Features.Auction.Hubs;
using TrustSimBackend.Application.Features.Auction.Services;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Commands;

public class AdminCancelAuctionCommandHandler : IRequestHandler<AdminCancelAuctionCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerService _ledgerService;
    private readonly IAuctionRealtimeNotifier _realtimeNotifier;
    private readonly IAuctionDelayQueueService _delayQueue;
    private readonly INotificationService _notificationService;

    public AdminCancelAuctionCommandHandler(
        IUnitOfWork unitOfWork,
        ILedgerService ledgerService,
        IAuctionRealtimeNotifier realtimeNotifier,
        IAuctionDelayQueueService delayQueue,
        INotificationService notificationService)
    {
        _unitOfWork = unitOfWork;
        _ledgerService = ledgerService;
        _realtimeNotifier = realtimeNotifier;
        _delayQueue = delayQueue;
        _notificationService = notificationService;
    }

    public async Task<bool> Handle(AdminCancelAuctionCommand request, CancellationToken cancellationToken)
    {
        var auction = await _unitOfWork.Auctions.GetDetailByIdAsync(request.AuctionId, cancellationToken);
        if (auction == null)
            throw new NotFoundException($"Không tìm thấy phiên đấu giá với ID '{request.AuctionId}'.");

        if (auction.AuctionStatus == AuctionStatus.CANCELLED)
        {
            throw new BadRequestException("Phiên đấu giá này đã bị hủy trước đó.");
        }

        var now = DateTime.UtcNow;
        auction.AuctionStatus = AuctionStatus.CANCELLED;
        auction.IsActive = false;
        auction.UpdatedDate = now;

        // 1. Hủy các timers trong Delay Queue
        await _delayQueue.CancelJobAsync(auction.Id, AuctionDelayJobTypes.AUCTION_START, cancellationToken);
        await _delayQueue.CancelJobAsync(auction.Id, AuctionDelayJobTypes.AUCTION_END, cancellationToken);
        await _delayQueue.CancelJobAsync(auction.Id, AuctionDelayJobTypes.DUTCH_PRICE_DROP, cancellationToken);
        await _delayQueue.CancelJobAsync(auction.Id, AuctionDelayJobTypes.PAYMENT_DEADLINE, cancellationToken);
        await _delayQueue.CancelJobAsync(auction.Id, AuctionDelayJobTypes.RUNNER_UP_DEADLINE, cancellationToken);
        await _delayQueue.CancelJobAsync(auction.Id, AuctionDelayJobTypes.SELLER_DECISION_DEADLINE, cancellationToken);

        // 2. Trả SIM về AVAILABLE
        var sim = await _unitOfWork.SimNumbers.GetByIdAsync(auction.SimNumberId, cancellationToken);
        if (sim != null)
        {
            sim.SimStatus = SimStatus.AVAILABLE;
            _unitOfWork.SimNumbers.Update(sim);
        }

        // 3. Hoàn cọc & mở khóa ví cho toàn bộ người tham gia
        foreach (var participant in auction.Participants)
        {
            participant.Status = ParticipantStatus.REFUNDED;
            _unitOfWork.Auctions.UpdateParticipant(participant);

            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(participant.UserAccountId, cancellationToken);
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
                    BalanceBefore = wallet.AvailableBalance - deposit.DepositAmount,
                    BalanceAfter = wallet.AvailableBalance,
                    LedgerJournalId = relJournal.Id,
                    JournalCode = relJournal.JournalCode,
                    CreatedAt = now,
                    CompletedAt = now
                };
                await _unitOfWork.AuctionTransactions.AddAsync(refundTx, cancellationToken);

                // Gửi thông báo hoàn cọc do Admin hủy phiên
                await _notificationService.SendNotificationAsync(
                    userId: participant.UserAccountId,
                    type: NotificationType.AUCTION_DEPOSIT_REFUNDED,
                    title: $"ℹ️ Hoàn tiền cọc phiên {auction.AuctionCode}",
                    content: $"Phiên đấu giá SIM {auction.SimNumber?.PhoneNumber} đã bị Quản trị viên hủy bỏ (Lý do: {request.Reason}). Tiền cọc {deposit.DepositAmount:N0} VNĐ của bạn đã được giải phóng hoàn trả về ví.",
                    referenceType: "Auction",
                    referenceId: auction.Id,
                    sendEmail: true,
                    cancellationToken: cancellationToken);
            }

            if (wallet != null)
            {
                wallet.WalletStatus = WalletStatus.ACTIVE;
                _unitOfWork.Wallets.Update(wallet);
            }
        }

        // 4. Hoàn cọc cam kết bán cho Người bán khi Admin hủy phiên
        if (auction.AuctionRule?.SellerDepositAmount > 0)
        {
            var sellerWallet = await _unitOfWork.Wallets.GetByUserIdAsync(auction.AuthorId, cancellationToken);
            if (sellerWallet != null)
            {
                var sellerDeposit = auction.AuctionRule.SellerDepositAmount;
                var relJournal = await _ledgerService.PostSellerListingDepositReleaseAsync(
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
                await _unitOfWork.AuctionTransactions.AddAsync(sellerRelTx, cancellationToken);
            }
        }

        // 5. Lưu hoặc cập nhật AuctionResult sang CANCELLED
        var auctionResult = await _unitOfWork.Auctions.GetResultByAuctionIdAsync(auction.Id, cancellationToken);
        if (auctionResult != null)
        {
            auctionResult.WinnerId = null;
            auctionResult.WinType = AuctionWinType.NONE;
            auctionResult.AuctionResultStatus = AuctionResultStatus.CANCELLED;
            auctionResult.CancelledAt = now;
            auctionResult.CancelledReason = request.Reason;
            _unitOfWork.Auctions.UpdateResult(auctionResult);
        }
        else
        {
            var newResult = new AuctionResult
            {
                Id = Guid.NewGuid(),
                AuctionId = auction.Id,
                WinnerId = null,
                FinalPrice = auction.CurrentPrice,
                WinType = AuctionWinType.NONE,
                AuctionResultStatus = AuctionResultStatus.CANCELLED,
                DeterminedAt = now,
                ConfirmedAt = now,
                CancelledAt = now,
                CancelledReason = request.Reason
            };
            await _unitOfWork.Auctions.AddResultAsync(newResult, cancellationToken);
        }

        _unitOfWork.Auctions.Update(auction);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Gửi thông báo cho Seller
        await _notificationService.SendNotificationAsync(
            userId: auction.AuthorId,
            type: NotificationType.AUCTION_FAILED,
            title: $"⚠️ Phiên đấu giá SIM {auction.SimNumber?.PhoneNumber} đã bị hủy bởi Quản trị viên",
            content: $"Phiên đấu giá {auction.AuctionCode} đã bị hủy bỏ bởi Quản trị viên. Lý do: {request.Reason}. SIM đã được chuyển về trạng thái Sẵn sàng.",
            referenceType: "Auction",
            referenceId: auction.Id,
            sendEmail: true,
            cancellationToken: cancellationToken);

        await _realtimeNotifier.NotifyAuctionFailedAsync(
            auction.Id,
            $"Phiên đấu giá đã bị Admin hủy vì lý do: {request.Reason}",
            cancellationToken);

        return true;
    }
}
