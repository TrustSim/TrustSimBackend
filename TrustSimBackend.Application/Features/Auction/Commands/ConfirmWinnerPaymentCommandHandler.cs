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
    private readonly IPdfContractService _pdfContractService;

    public ConfirmWinnerPaymentCommandHandler(
        IUnitOfWork unitOfWork,
        ILedgerService ledgerService,
        IAuctionRealtimeNotifier realtimeNotifier,
        IAuctionDelayQueueService delayQueue,
        INotificationService notificationService,
        IPdfContractService pdfContractService)
    {
        _unitOfWork = unitOfWork;
        _ledgerService = ledgerService;
        _realtimeNotifier = realtimeNotifier;
        _delayQueue = delayQueue;
        _notificationService = notificationService;
        _pdfContractService = pdfContractService;
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

        var lockedDeposits = participant.Deposits.Where(d => d.DepositStatus == DepositStatus.LOCKED).ToList();
        var totalDepositPaid = lockedDeposits.Sum(d => d.DepositAmount);
        var totalEscrowAmount = auction.CurrentPrice;
        var remainingRequired = totalEscrowAmount - totalDepositPaid;

        if (wallet.AvailableBalance < remainingRequired)
        {
            throw new BadRequestException(
                $"Số dư khả dụng trong ví ({wallet.AvailableBalance:N0} VNĐ) không đủ để thanh toán số tiền còn lại ({remainingRequired:N0} VNĐ). Vui lòng nạp thêm tiền vào ví.");
        }

        var now = DateTime.UtcNow;
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
            BuyerId = participant.UserAccountId,
            SellerId = auction.AuthorId,
            SimNumberId = auction.SimNumberId,
            EscrowDeadline = now.AddDays(2),
            SigningDeadline = signingDeadline,
            TransferDeadline = now.AddDays(7),
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
            EscrowAmount = totalEscrowAmount,
            EscrowStatus = EscrowStatus.HELD,
            AuctionDepositId = lockedDeposits.FirstOrDefault()?.Id,
            HeldAt = now
        };

        // Ghi nhận chứng từ sổ cái kép ESCROW_HOLD (Bút toán 3 dòng nguyên tử)
        var journal = await _ledgerService.PostEscrowHoldAsync(
            wallet.Id,
            totalEscrowAmount,
            totalDepositPaid,
            escrowId,
            cancellationToken);

        // Cập nhật trạng thái tất cả các khoản cọc sang APPLIED_TO_ESCROW
        foreach (var dep in lockedDeposits)
        {
            dep.DepositStatus = DepositStatus.APPLIED_TO_ESCROW;
            dep.AppliedtoEscrowedAt = now;
            dep.Escrow = escrow;
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

        // Hủy deadline thanh toán trong Redis Delay Queue và nạp deadline ký hợp đồng
        await _delayQueue.CancelJobAsync(auction.Id, AuctionDelayJobTypes.PAYMENT_DEADLINE, cancellationToken);
        await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
        {
            AuctionId = auction.Id,
            JobType = AuctionDelayJobTypes.SIGNING_DEADLINE,
            ExecuteAt = trade.SigningDeadline,
            ExtraData = trade.Id.ToString()
        }, cancellationToken);

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

        // Tự động sinh Văn bản 1 (Phiếu chốt giá trúng) và tải lên Supabase Storage CDN ngay lập tức
        string? winningNoticeUrl = null;
        try
        {
            winningNoticeUrl = await _pdfContractService.EnsureWinningNoticeUploadedAsync(trade.Id, cancellationToken);
        }
        catch
        {
            // fallback nếu có lỗi bất ngờ
        }

        // Gửi thông báo cho Người thắng và Người bán
        var buyerName = participant.UserAccount?.UserName ?? "Người thắng cuộc";
        var graceHours = auction.AuctionRule?.SigningGracePeriodHours ?? 24;

        await _notificationService.SendNotificationAsync(
            userId: participant.UserAccountId,
            type: NotificationType.CONTRACT_SIGNING_REQUIRED,
            title: $"📝 Yêu cầu ký hợp đồng SIM {sim?.PhoneNumber}!",
            content: $"Bạn đã thanh toán thành công {remainingRequired:N0} VNĐ cho phiên {auction.AuctionCode}. Tiền đã vào quỹ Escrow bảo đảm an toàn. Vui lòng xem biên bản chốt giá trúng và ký hợp đồng điện tử trong vòng {graceHours} giờ.",
            actionUrl: winningNoticeUrl ?? $"/trades/{trade.Id}",
            referenceType: "TradeTransaction",
            referenceId: trade.Id,
            sendEmail: true,
            cancellationToken: cancellationToken);

        await _notificationService.SendNotificationAsync(
            userId: auction.AuthorId,
            type: NotificationType.CONTRACT_SIGNING_REQUIRED,
            title: $"📝 Người mua {buyerName} đã thanh toán SIM {sim?.PhoneNumber}! Vui lòng ký hợp đồng",
            content: $"Người thắng ({buyerName}) phiên {auction.AuctionCode} đã thanh toán đủ {totalEscrowAmount:N0} VNĐ vào quỹ Escrow. Vui lòng xem biên bản chốt giá trúng và ký hợp đồng điện tử trong vòng {graceHours} giờ.",
            actionUrl: winningNoticeUrl ?? $"/trades/{trade.Id}",
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
