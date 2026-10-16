using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Helpers;
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
    private readonly INotificationService _notificationService;
    private readonly IPdfContractService _pdfContractService;

    public AcceptDutchBuyCommandHandler(
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

        var graceMinutes = rule?.PaymentGracePeriodMinutes > 0
            ? rule.PaymentGracePeriodMinutes
            : (rule?.PaymentGracePeriodHours > 0 ? rule.PaymentGracePeriodHours * 60 : 1440);
        var paymentDeadline = now.AddMinutes(graceMinutes);

        var resultEntity = new AuctionResult
        {
            Id = Guid.NewGuid(),
            AuctionId = auction.Id,
            InitialWinnerId = participant.Id,
            InitialWinningPrice = currentPrice,
            WinnerId = participant.Id,
            FinalPrice = currentPrice,
            RunnerUpParticipantId = null,
            RunnerUpFinalPrice = null,
            WinType = AuctionWinType.NONE,
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

                // Gửi thông báo hoàn cọc cho người tham gia khác
                await _notificationService.SendNotificationAsync(
                    userId: p.UserAccountId,
                    type: NotificationType.AUCTION_DEPOSIT_REFUNDED,
                    title: $"💵 Hoàn cọc phiên Hà Lan {auction.AuctionCode}",
                    content: $"Phiên đấu giá SIM {auction.SimNumber?.PhoneNumber} đã kết thúc do có người mua ngay. Khoản tiền cọc {dep.DepositAmount:N0} VNĐ của bạn đã được hoàn trả về ví khả dụng.",
                    referenceType: "Auction",
                    referenceId: auction.Id,
                    cancellationToken: cancellationToken);
            }

            if (w != null)
            {
                w.WalletStatus = WalletStatus.ACTIVE;
                _unitOfWork.Wallets.Update(w);
            }
        }

        // 2. Quyết toán Người thắng: Nếu đủ tiền -> Tự động quyết toán ngay; Nếu thiếu -> Chờ thời hạn (Grace Period)
        var winnerWallet = await _unitOfWork.Wallets.GetByUserIdAsync(participant.UserAccountId, cancellationToken);
        var buyerName = participant.UserAccount?.UserName ?? "Người mua";

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
                    TradeTransactionStatus = TradeTransactionStatus.PendingSignature,
                    AuctionId = auction.Id,
                    BuyerId = participant.UserAccountId,
                    SellerId = auction.AuthorId,
                    SimNumberId = auction.SimNumberId,
                    EscrowDeadline = null,
                    SigningDeadline = now.AddMinutes(auction.AuctionRule?.SigningGracePeriodMinutes ?? 1440),
                    TransferDeadline = null,
                    LiquidationDeadline = null,
                    CompletedAt = null,
                    CancelledAt = null,
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
                resultEntity.WinType = AuctionWinType.DUTCH_BUY_NOW;
                resultEntity.SettledAt = now;

                await _unitOfWork.Escrows.AddAsync(escrow, cancellationToken);
                await _unitOfWork.TradeTransactions.AddAsync(trade, cancellationToken);
                await _unitOfWork.AuctionTransactions.AddAsync(auctionTx, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                // Tự động sinh Văn bản 1 (Phiếu chốt giá trúng) và upload Supabase Storage CDN ngay lập tức
                string? winningNoticeUrl = null;
                try
                {
                    winningNoticeUrl = await _pdfContractService.EnsureWinningNoticeUploadedAsync(trade.Id, cancellationToken);
                }
                catch
                {
                    // fallback nếu lỗi
                }

                // Thông báo cho Người mua và Người bán
                await _notificationService.SendNotificationAsync(
                    userId: participant.UserAccountId,
                    type: NotificationType.AUCTION_WON,
                    title: $"🎉 Chúc mừng bạn đã mua thành công SIM {sim?.PhoneNumber}!",
                    content: $"Bạn đã mua thành công SIM {sim?.PhoneNumber} trong phiên đấu giá Hà Lan {auction.AuctionCode} với mức giá {currentPrice:N0} VNĐ. Toàn bộ số tiền đã được chuyển vào quỹ Escrow bảo đảm.",
                    actionUrl: winningNoticeUrl ?? $"/trades/{trade.Id}",
                    referenceType: "TradeTransaction",
                    referenceId: trade.Id,
                    sendEmail: true,
                    cancellationToken: cancellationToken);

                await _notificationService.SendNotificationAsync(
                    userId: auction.AuthorId,
                    type: NotificationType.TRADE_TRANSFERRING_SIM,
                    title: $"🎉 Phiên đấu giá Hà Lan SIM {auction.SimNumber?.PhoneNumber} đã có người mua!",
                    content: $"Phiên đấu giá {auction.AuctionCode} đã kết thúc. Người mua là {buyerName} với mức giá {currentPrice:N0} VNĐ (Đã thanh toán đủ vào quỹ Escrow). Vui lòng tiến hành thủ tục chuyển nhượng.",
                    actionUrl: winningNoticeUrl ?? $"/trades/{trade.Id}",
                    referenceType: "TradeTransaction",
                    referenceId: trade.Id,
                    sendEmail: true,
                    cancellationToken: cancellationToken);
            }
            else
            {
                // Thiếu tiền -> Lập lịch Payment Deadline trong Redis Delay Queue và mở ví người thắng để nạp tiền
                winnerWallet.WalletStatus = WalletStatus.ACTIVE;
                _unitOfWork.Wallets.Update(winnerWallet);

                resultEntity.AuctionResultStatus = AuctionResultStatus.PENDING_PAYMENT;
                resultEntity.WinType = AuctionWinType.NONE;
                resultEntity.PaymentDeadline = paymentDeadline;

                await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
                {
                    AuctionId = auction.Id,
                    JobType = AuctionDelayJobTypes.PAYMENT_DEADLINE,
                    ExecuteAt = paymentDeadline
                }, cancellationToken);

                // Thông báo cho Người mua và Người bán
                await _notificationService.SendNotificationAsync(
                    userId: participant.UserAccountId,
                    type: NotificationType.AUCTION_WON,
                    title: $"🎉 Bạn đã trúng quyền mua SIM {auction.SimNumber?.PhoneNumber}!",
                    content: $"Mức giá mua của bạn trong phiên đấu giá Hà Lan là {currentPrice:N0} VNĐ. Vui lòng nạp thêm và hoàn tất thanh toán trước {paymentDeadline.ToVietnamTimeString("HH:mm dd/MM/yyyy")}.",
                    actionUrl: $"/auctions/{auction.Id}/payment",
                    referenceType: "Auction",
                    referenceId: auction.Id,
                    sendEmail: true,
                    cancellationToken: cancellationToken);

                await _notificationService.SendNotificationAsync(
                    userId: auction.AuthorId,
                    type: NotificationType.SYSTEM_ALERT,
                    title: $"ℹ️ Phiên Hà Lan SIM {auction.SimNumber?.PhoneNumber} đã kết thúc (Chờ thanh toán)",
                    content: $"Phiên đấu giá {auction.AuctionCode} đã kết thúc. Người mua là {buyerName} với mức giá {currentPrice:N0} VNĐ (Chưa thanh toán - Hạn thanh toán đến {paymentDeadline.ToVietnamTimeString("HH:mm dd/MM/yyyy")}). Hệ thống sẽ tự động thông báo khi người mua thanh toán hoặc quá hạn.",
                    referenceType: "Auction",
                    referenceId: auction.Id,
                    sendEmail: true,
                    cancellationToken: cancellationToken);
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
            WinnerUserName = buyerName,
            FinalPrice = resultEntity.FinalPrice,
            InitialWinnerId = resultEntity.InitialWinnerId,
            InitialWinningPrice = resultEntity.InitialWinningPrice,
            WinType = resultEntity.WinType,
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
