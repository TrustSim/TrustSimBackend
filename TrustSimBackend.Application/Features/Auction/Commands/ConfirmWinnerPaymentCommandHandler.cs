using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;
using TrustSimBackend.Application.Features.Auction.Hubs;
using TrustSimBackend.Application.Features.Auction.Services;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Commands;

public class ConfirmWinnerPaymentCommandHandler : IRequestHandler<ConfirmWinnerPaymentCommand, AuctionResultDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerService _ledgerService;
    private readonly IAuctionRealtimeNotifier _realtimeNotifier;
    private readonly IAuctionDelayQueueService _delayQueue;
    private readonly INotificationService _notificationService;

    public ConfirmWinnerPaymentCommandHandler(
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

    public async Task<AuctionResultDto> Handle(ConfirmWinnerPaymentCommand request, CancellationToken cancellationToken)
    {
        var auction = await _unitOfWork.Auctions.GetByIdAsync(request.AuctionId, cancellationToken);
        if (auction == null)
        {
            throw new NotFoundException($"Không tìm thấy phiên đấu giá với ID '{request.AuctionId}'.");
        }

        var participant = await _unitOfWork.Auctions.GetParticipantAsync(request.AuctionId, request.UserId, cancellationToken);
        if (participant == null)
        {
            throw new ForbiddenAccessException("Bạn không phải là người tham gia phiên đấu giá này.");
        }

        if (participant.Status != ParticipantStatus.WON)
        {
            throw new BadRequestException("Bạn không phải là người chiến thắng của phiên đấu giá này.");
        }

        var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(request.UserId, cancellationToken);
        if (wallet == null)
        {
            throw new NotFoundException("Không tìm thấy ví tiền của người dùng.");
        }

        var deposit = participant.Deposits.FirstOrDefault(d => d.DepositStatus == DepositStatus.LOCKED);
        var depositPaid = deposit?.DepositAmount ?? 0m;
        var totalEscrowAmount = auction.CurrentPrice;
        var remainingRequired = totalEscrowAmount - depositPaid;

        if (wallet.AvailableBalance < remainingRequired)
        {
            throw new BadRequestException(
                $"Số dư khả dụng trong ví ({wallet.AvailableBalance:N0} VNĐ) không đủ để thanh toán số tiền còn lại ({remainingRequired:N0} VNĐ). Vui lòng nạp thêm tiền vào ví.");
        }

        var now = DateTime.UtcNow;
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
            EscrowAmount = totalEscrowAmount,
            EscrowStatus = EscrowStatus.HELD,
            AuctionDepositId = deposit?.Id,
            HeldAt = now
        };

        // Ghi nhận chứng từ sổ cái kép ESCROW_HOLD (Bút toán 3 dòng nguyên tử)
        var journal = await _ledgerService.PostEscrowHoldAsync(
            wallet.Id,
            totalEscrowAmount,
            depositPaid,
            escrowId,
            cancellationToken);

        // Cập nhật trạng thái cọc sang APPLIED_TO_ESCROW
        if (deposit != null)
        {
            deposit.DepositStatus = DepositStatus.APPLIED_TO_ESCROW;
            deposit.AppliedtoEscrowedAt = now;
            deposit.Escrow = escrow;
        }

        // Tạo bản ghi AuctionTransaction
        var auctionTx = new AuctionTransaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            AuctionId = auction.Id,
            EscrowId = escrowId,
            AuctionTransactionType = AuctionTransactionType.ESCROW_HOLD,
            TransactionStatus = TransactionStatus.COMPLETED,
            Amount = totalEscrowAmount,
            BalanceBefore = wallet.AvailableBalance + remainingRequired,
            BalanceAfter = wallet.AvailableBalance,
            LedgerJournalId = journal.Id,
            JournalCode = journal.JournalCode,
            CreatedAt = now,
            CompletedAt = now
        };

        // Gán quan hệ 2 chiều
        trade.Escrow = escrow;

        // Hủy deadline trong Redis Delay Queue
        await _delayQueue.CancelJobAsync(auction.Id, AuctionDelayJobTypes.PAYMENT_DEADLINE, cancellationToken);

        // Cập nhật trạng thái SIM sang TRANSFERRING
        var sim = await _unitOfWork.SimNumbers.GetByIdAsync(auction.SimNumberId, cancellationToken);
        if (sim != null)
        {
            sim.SimStatus = SimStatus.TRANSFERRING;
            _unitOfWork.SimNumbers.Update(sim);
        }

        // Đảm bảo ví giữ trạng thái ACTIVE
        wallet.WalletStatus = WalletStatus.ACTIVE;
        _unitOfWork.Wallets.Update(wallet);

        await _unitOfWork.Escrows.AddAsync(escrow, cancellationToken);
        await _unitOfWork.TradeTransactions.AddAsync(trade, cancellationToken);
        await _unitOfWork.AuctionTransactions.AddAsync(auctionTx, cancellationToken);

        var auctionResult = await _unitOfWork.Auctions.GetResultByAuctionIdAsync(auction.Id, cancellationToken);
        if (auctionResult != null)
        {
            auctionResult.WinnerId = participant.Id;
            auctionResult.FinalPrice = auction.CurrentPrice;
            auctionResult.AuctionResultStatus = AuctionResultStatus.CONFIRMED;
            auctionResult.SettledAt = now;
            if (auctionResult.WinType == AuctionWinType.NONE)
            {
                auctionResult.WinType = auction.AuctionType == AuctionType.DUTCH 
                    ? AuctionWinType.DUTCH_BUY_NOW 
                    : AuctionWinType.ORIGINAL_WINNER;
            }
            if (!auctionResult.InitialWinnerId.HasValue)
            {
                auctionResult.InitialWinnerId = auctionResult.WinnerId;
                auctionResult.InitialWinningPrice = auctionResult.FinalPrice;
            }
            _unitOfWork.Auctions.UpdateResult(auctionResult);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Gửi thông báo cho Người thắng và Người bán
        var buyerName = participant.UserAccount?.UserName ?? "Người thắng cuộc";

        await _notificationService.SendNotificationAsync(
            userId: participant.UserAccountId,
            type: NotificationType.AUCTION_WON,
            title: $"🎉 Thanh toán thành công SIM {sim?.PhoneNumber}!",
            content: $"Bạn đã hoàn tất thanh toán {remainingRequired:N0} VNĐ cho phiên {auction.AuctionCode}. Toàn bộ số tiền đã được Escrow bảo đảm an toàn. Đang chờ chuyển giao SIM.",
            actionUrl: $"/trades/{trade.Id}",
            referenceType: "TradeTransaction",
            referenceId: trade.Id,
            sendEmail: true,
            cancellationToken: cancellationToken);

        await _notificationService.SendNotificationAsync(
            userId: auction.AuthorId,
            type: NotificationType.TRADE_TRANSFERRING_SIM,
            title: $"💰 Người mua {buyerName} đã thanh toán xong SIM {sim?.PhoneNumber}!",
            content: $"Người thắng ({buyerName}) phiên {auction.AuctionCode} đã thanh toán đủ {totalEscrowAmount:N0} VNĐ vào quỹ Escrow. Vui lòng tiến hành thủ tục sang tên SIM.",
            actionUrl: $"/trades/{trade.Id}",
            referenceType: "TradeTransaction",
            referenceId: trade.Id,
            sendEmail: true,
            cancellationToken: cancellationToken);

        var resultDto = new AuctionResultDto
        {
            Id = auctionResult?.Id ?? Guid.Empty,
            AuctionId = auction.Id,
            WinnerId = participant.Id,
            WinnerUserName = buyerName,
            FinalPrice = auction.CurrentPrice,
            InitialWinnerId = auctionResult?.InitialWinnerId ?? participant.Id,
            InitialWinningPrice = auctionResult?.InitialWinningPrice ?? auction.CurrentPrice,
            WinType = auctionResult?.WinType ?? (auction.AuctionType == AuctionType.DUTCH ? AuctionWinType.DUTCH_BUY_NOW : AuctionWinType.ORIGINAL_WINNER),
            AuctionResultStatus = AuctionResultStatus.CONFIRMED,
            SettledAt = now
        };

        await _realtimeNotifier.NotifyAuctionStatusChangedAsync(
            auction.Id,
            AuctionResultStatus.CONFIRMED.ToString(),
            participant.Id,
            auction.CurrentPrice,
            cancellationToken);

        return resultDto;
    }
}
