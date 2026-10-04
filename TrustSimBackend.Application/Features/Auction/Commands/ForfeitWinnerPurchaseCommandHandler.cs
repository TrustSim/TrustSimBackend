using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
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
    private readonly IAuctionRealtimeNotifier _realtimeNotifier;
    private readonly IAuctionDelayQueueService _delayQueue;

    public ForfeitWinnerPurchaseCommandHandler(
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

        var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(request.UserId, cancellationToken);
        if (wallet == null)
            throw new NotFoundException("Không tìm thấy ví tiền của người dùng.");

        var now = DateTime.UtcNow;

        // 1. Hủy ngay lập tức job hẹn giờ Payment Deadline trong Redis Delay Queue
        await _delayQueue.CancelJobAsync(auction.Id, AuctionDelayJobTypes.PAYMENT_DEADLINE, cancellationToken);

        // 2. Xử lý phạt cọc Người thắng chủ động từ bỏ mua SIM
        participant.Status = ParticipantStatus.DEFAULTED;
        _unitOfWork.Auctions.UpdateParticipant(participant);

        var deposit = participant.Deposits.FirstOrDefault(d => d.DepositStatus == DepositStatus.LOCKED);
        if (deposit != null)
        {
            deposit.DepositStatus = DepositStatus.FORFEITED;
            deposit.ForfeitedAt = now;
            deposit.ForfeitedReason = string.IsNullOrWhiteSpace(request.Reason)
                ? "Người thắng chủ động từ bỏ quyền mua SIM sau đấu giá."
                : request.Reason;

            var forfeitJournal = await _ledgerService.PostAuctionDepositForfeitedAsync(
                wallet.Id,
                deposit.DepositAmount,
                deposit.Id,
                cancellationToken);

            var forfeitureTx = new AuctionTransaction
            {
                Id = Guid.NewGuid(),
                WalletId = wallet.Id,
                AuctionId = auction.Id,
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
            await _unitOfWork.AuctionTransactions.AddAsync(forfeitureTx, cancellationToken);
        }

        // 3. Cập nhật Bid của Winner cũ sang REJECTED
        var bids = await _unitOfWork.Bids.GetBidsByAuctionIdAsync(auction.Id, top: 100, cancellationToken);
        var winnerBid = bids.FirstOrDefault(b => b.ParticipantId == participant.Id && b.Status == BidStatus.WINNING);
        if (winnerBid != null)
        {
            winnerBid.Status = BidStatus.REJECTED;
            _unitOfWork.Bids.Update(winnerBid);
        }

        // 4. Chuyển giao quyền mua cho Người về nhì (nếu có)
        if (auctionResult.RunnerUpParticipantId.HasValue && auctionResult.RunnerUpFinalPrice.HasValue)
        {
            var graceHours = auction.AuctionRule?.PaymentGracePeriodHours ?? 24;
            var runnerUpDeadline = now.AddHours(graceHours);

            auctionResult.AuctionResultStatus = AuctionResultStatus.RUNNER_UP_PENDING;
            auctionResult.IsRunnerUpOffered = true;
            auctionResult.RunnerUpPaymentDeadline = runnerUpDeadline;
            _unitOfWork.Auctions.UpdateResult(auctionResult);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Lập lịch hạn chót cho Runner-Up
            await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
            {
                AuctionId = auction.Id,
                JobType = AuctionDelayJobTypes.RUNNER_UP_DEADLINE,
                ExecuteAt = runnerUpDeadline
            }, cancellationToken);

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

        // 5. Nếu Không có Người về nhì -> Phiên FAILED, trả SIM về AVAILABLE
        auctionResult.AuctionResultStatus = AuctionResultStatus.FAILED;
        auctionResult.CancelledAt = now;
        auctionResult.CancelledReason = "Người thắng chủ động từ bỏ quyền mua SIM và không có Người về nhì.";
        _unitOfWork.Auctions.UpdateResult(auctionResult);

        var sim = await _unitOfWork.SimNumbers.GetByIdAsync(auction.SimNumberId, cancellationToken);
        if (sim != null)
        {
            sim.SimStatus = SimStatus.AVAILABLE;
            _unitOfWork.SimNumbers.Update(sim);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _realtimeNotifier.NotifyAuctionFailedAsync(
            auction.Id,
            "Người thắng đã chủ động từ bỏ quyền mua SIM và phiên không có Người về nhì hợp lệ. Phiên kết thúc thất bại.",
            cancellationToken);

        return new AuctionResultDto
        {
            Id = auctionResult.Id,
            AuctionId = auction.Id,
            WinnerId = auctionResult.WinnerId,
            FinalPrice = auctionResult.FinalPrice,
            AuctionResultStatus = AuctionResultStatus.FAILED,
            CancelledReason = auctionResult.CancelledReason
        };
    }
}
