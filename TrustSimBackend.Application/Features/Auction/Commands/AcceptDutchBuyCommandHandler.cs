using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;
using TrustSimBackend.Application.Features.Auction.Hubs;
using TrustSimBackend.Application.Features.Auction.Services;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Commands;

public class AcceptDutchBuyCommandHandler : IRequestHandler<AcceptDutchBuyCommand, AuctionResultDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerService _ledgerService;
    private readonly IAuctionRealtimeNotifier _realtimeNotifier;
    private readonly IAuctionDelayQueueService _delayQueue;

    public AcceptDutchBuyCommandHandler(
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

    public async Task<AuctionResultDto> Handle(AcceptDutchBuyCommand request, CancellationToken cancellationToken)
    {
        var auction = await _unitOfWork.Auctions.GetByIdAsync(request.AuctionId, cancellationToken);
        if (auction == null)
        {
            throw new NotFoundException($"Không tìm thấy phiên đấu giá với ID '{request.AuctionId}'.");
        }

        if (auction.AuctionType != AuctionType.DUTCH)
        {
            throw new BadRequestException("Phiên đấu giá này không phải là loại hình đấu giá giảm dần (Dutch Auction).");
        }

        if (auction.AuctionStatus != AuctionStatus.ACTIVE)
        {
            throw new BadRequestException("Phiên đấu giá chưa mở hoặc đã kết thúc/đã có người mua.");
        }

        if (auction.AuthorId == request.UserId)
        {
            throw new BadRequestException("Người tạo phiên đấu giá không thể tự mua số SIM của mình.");
        }

        var participant = await _unitOfWork.Auctions.GetParticipantAsync(request.AuctionId, request.UserId, cancellationToken);
        if (participant == null)
        {
            throw new BadRequestException("Bạn chưa đăng ký đặt cọc tham gia phiên đấu giá này.");
        }

        if (participant.Status == ParticipantStatus.BANNED)
        {
            throw new ForbiddenAccessException("Tài khoản của bạn đã bị khóa quyền tham gia trả giá trong phiên này.");
        }

        var rule = auction.AuctionRule;
        var interval = rule?.DutchDecreaseIntervalSeconds > 0 ? rule.DutchDecreaseIntervalSeconds : 60;
        var decreaseAmount = rule?.DutchDecreaseAmount ?? 0m;
        var now = DateTime.UtcNow;

        var elapsed = (now - auction.StartAt).TotalSeconds;
        var cycles = Math.Max(0, (int)(elapsed / interval));
        var calculatedPrice = auction.StartingPrice - (cycles * decreaseAmount);
        var currentPrice = Math.Max(calculatedPrice, auction.ReservePrice);

        // Khóa chốt phiên
        auction.AuctionStatus = AuctionStatus.END;
        auction.IsActive = false;
        auction.CurrentPrice = currentPrice;
        auction.LockVersion++;

        // Hủy job đếm nhịp hạ giá trong Redis Delay Queue
        await _delayQueue.CancelJobAsync(auction.Id, AuctionDelayJobTypes.DUTCH_PRICE_DROP, cancellationToken);

        // Tạo bản ghi Bid chiến thắng
        var winningBid = new Bid
        {
            Id = Guid.NewGuid(),
            AuctionId = auction.Id,
            ParticipantId = participant.Id,
            BidAmount = currentPrice,
            PlacedAt = now,
            Status = BidStatus.WINNING
        };
        await _unitOfWork.Bids.AddAsync(winningBid, cancellationToken);

        participant.Status = ParticipantStatus.WON;
        participant.AcceptedBidCount++;
        _unitOfWork.Auctions.UpdateParticipant(participant);

        var graceHours = rule?.PaymentGracePeriodHours ?? 24;
        var paymentDeadline = now.AddHours(graceHours);

        var resultEntity = new AuctionResult
        {
            Id = Guid.NewGuid(),
            AuctionId = auction.Id,
            WinnerId = participant.Id,
            FinalPrice = currentPrice,
            RunnerUpParticipantId = null,
            RunnerUpFinalPrice = null,
            AuctionResultStatus = AuctionResultStatus.PENDING_PAYMENT,
            PaymentDeadline = paymentDeadline,
            DeterminedAt = now,
            ConfirmedAt = now
        };

        // 1. Hoàn cọc & ghi nhận Sổ cái cho các người tham gia khác (người thua)
        foreach (var p in auction.Participants)
        {
            if (p.Id == participant.Id) continue;

            p.Status = ParticipantStatus.REFUNDED;
            _unitOfWork.Auctions.UpdateParticipant(p);

            var w = await _unitOfWork.Wallets.GetByUserIdAsync(p.UserAccountId, cancellationToken);
            var dep = p.Deposits.FirstOrDefault(d => d.DepositStatus == DepositStatus.LOCKED);

            if (w != null && dep != null)
            {
                dep.DepositStatus = DepositStatus.RELEASED;
                dep.ReleasedAT = now;

                var relJournal = await _ledgerService.PostAuctionDepositReleaseAsync(
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
                await _unitOfWork.AuctionTransactions.AddAsync(refundTx, cancellationToken);
            }

            if (w != null)
            {
                w.WalletStatus = WalletStatus.ACTIVE;
                _unitOfWork.Wallets.Update(w);
            }
        }

        // 2. Quyết toán Người thắng: Nếu đủ tiền -> Tự động quyết toán ngay; Nếu thiếu -> Chờ thời hạn (Grace Period)
        var winnerWallet = await _unitOfWork.Wallets.GetByUserIdAsync(participant.UserAccountId, cancellationToken);
        if (winnerWallet != null)
        {
            var winnerDeposit = participant.Deposits.FirstOrDefault(d => d.DepositStatus == DepositStatus.LOCKED);
            var depositPaid = winnerDeposit?.DepositAmount ?? 0m;
            var remainingRequired = currentPrice - depositPaid;

            if (winnerWallet.AvailableBalance >= remainingRequired)
            {
                // Đủ tiền -> Tự động quyết toán Escrow ngay lập tức
                var escrowId = Guid.NewGuid();
                var tradeId = Guid.NewGuid();

                var trade = new TradeTransaction
                {
                    Id = tradeId,
                    TradeTransactionCode = $"TRD-{now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
                    TradeTransactionStatus = TradeTransactionStatus.PendingTransfer,
                    AuctionId = auction.Id,
                    BuyerId = participant.UserAccountId,
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
                    EscrowAmount = currentPrice,
                    EscrowStatus = EscrowStatus.HELD,
                    AuctionDepositId = winnerDeposit?.Id,
                    HeldAt = now
                };

                // Gán quan hệ 2 chiều
                trade.Escrow = escrow;

                // Ghi sổ cái kép ESCROW_HOLD (Bút toán 3 dòng nguyên tử)
                var journal = await _ledgerService.PostEscrowHoldAsync(
                    winnerWallet.Id,
                    currentPrice,
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
                    WalletId = winnerWallet.Id,
                    AuctionId = auction.Id,
                    EscrowId = escrowId,
                    AuctionTransactionType = AuctionTransactionType.ESCROW_HOLD,
                    TransactionStatus = TransactionStatus.COMPLETED,
                    Amount = currentPrice,
                    BalanceBefore = winnerWallet.AvailableBalance + remainingRequired,
                    BalanceAfter = winnerWallet.AvailableBalance,
                    LedgerJournalId = journal.Id,
                    JournalCode = journal.JournalCode,
                    CreatedAt = now,
                    CompletedAt = now
                };

                // Cập nhật trạng thái SIM sang TRANSFERRING
                var sim = await _unitOfWork.SimNumbers.GetByIdAsync(auction.SimNumberId, cancellationToken);
                if (sim != null)
                {
                    sim.SimStatus = SimStatus.TRANSFERRING;
                    _unitOfWork.SimNumbers.Update(sim);
                }

                // Đảm bảo mở khóa ví người thắng sang ACTIVE
                winnerWallet.WalletStatus = WalletStatus.ACTIVE;
                _unitOfWork.Wallets.Update(winnerWallet);

                resultEntity.AuctionResultStatus = AuctionResultStatus.CONFIRMED;
                resultEntity.SettledAt = now;

                await _unitOfWork.Escrows.AddAsync(escrow, cancellationToken);
                await _unitOfWork.TradeTransactions.AddAsync(trade, cancellationToken);
                await _unitOfWork.AuctionTransactions.AddAsync(auctionTx, cancellationToken);
            }
            else
            {
                // Thiếu tiền -> Lập lịch Payment Deadline trong Redis Delay Queue và mở ví người thắng để nạp tiền
                winnerWallet.WalletStatus = WalletStatus.ACTIVE;
                _unitOfWork.Wallets.Update(winnerWallet);

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

        await _unitOfWork.Auctions.AddResultAsync(resultEntity, cancellationToken);
        _unitOfWork.Auctions.Update(auction);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var resultDto = new AuctionResultDto
        {
            Id = resultEntity.Id,
            AuctionId = resultEntity.AuctionId,
            WinnerId = resultEntity.WinnerId,
            FinalPrice = resultEntity.FinalPrice,
            AuctionResultStatus = resultEntity.AuctionResultStatus,
            PaymentDeadline = resultEntity.PaymentDeadline,
            SettledAt = resultEntity.SettledAt,
            DeterminedAt = resultEntity.DeterminedAt,
            ConfirmedAt = resultEntity.ConfirmedAt
        };

        // Bắn SignalR kết thúc phiên
        await _realtimeNotifier.NotifyAuctionEndedAsync(auction.Id, resultDto, cancellationToken);

        return resultDto;
    }
}
