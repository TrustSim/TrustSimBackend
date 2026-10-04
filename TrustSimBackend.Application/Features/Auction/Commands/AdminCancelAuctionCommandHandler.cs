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

    public AdminCancelAuctionCommandHandler(
        IUnitOfWork unitOfWork,
        ILedgerService ledgerService,
        IAuctionRealtimeNotifier realtimeNotifier,
        IAuctionDelayQueueService delayQueue)
    {
        _unitOfWork = unitOfWork;
        _ledgerService = ledgerService;
        _realtimeNotifier = realtimeNotifier;
        _delayQueue = delayQueue;
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
            }

            if (wallet != null)
            {
                wallet.WalletStatus = WalletStatus.ACTIVE;
                _unitOfWork.Wallets.Update(wallet);
            }
        }

        // 4. Lưu hoặc cập nhật AuctionResult sang CANCELLED
        var auctionResult = await _unitOfWork.Auctions.GetResultByAuctionIdAsync(auction.Id, cancellationToken);
        if (auctionResult != null)
        {
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

        await _realtimeNotifier.NotifyAuctionFailedAsync(
            auction.Id,
            $"Phiên đấu giá đã bị Admin hủy vì lý do: {request.Reason}",
            cancellationToken);

        return true;
    }
}
