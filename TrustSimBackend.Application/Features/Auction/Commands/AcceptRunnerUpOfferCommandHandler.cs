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

public class AcceptRunnerUpOfferCommandHandler : IRequestHandler<AcceptRunnerUpOfferCommand, AuctionResultDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerService _ledgerService;
    private readonly IAuctionRealtimeNotifier _realtimeNotifier;
    private readonly IAuctionDelayQueueService _delayQueue;
    private readonly INotificationService _notificationService;
    private readonly IPdfContractService _pdfContractService;

    public AcceptRunnerUpOfferCommandHandler(
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

    public async Task<AuctionResultDto> Handle(AcceptRunnerUpOfferCommand request, CancellationToken cancellationToken)
    {
        var auction = await _unitOfWork.Auctions.GetByIdAsync(request.AuctionId, cancellationToken);
        if (auction == null)
        {
            throw new NotFoundException($"Không tìm thấy phiên đấu giá với ID '{request.AuctionId}'.");
        }

        var participant = await _unitOfWork.Auctions.GetParticipantAsync(request.AuctionId, request.UserId, cancellationToken);
        if (participant == null || participant.Status != ParticipantStatus.RUNNER_UP)
        {
            throw new ForbiddenAccessException("Bạn không phải là Người về nhì được nhận quyền mua trong phiên này.");
        }

        var auctionResult = await _unitOfWork.Auctions.GetResultByAuctionIdAsync(auction.Id, cancellationToken);
        if (auctionResult == null || auctionResult.AuctionResultStatus != AuctionResultStatus.RUNNER_UP_PENDING)
        {
            throw new BadRequestException("Phiên đấu giá không ở trạng thái chờ Người về nhì xác nhận mua.");
        }

        var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(request.UserId, cancellationToken);
        if (wallet == null)
        {
            throw new NotFoundException("Không tìm thấy ví tiền của người dùng.");
        }

        // Lấy danh sách lượt đặt giá của phiên
        var allBids = await _unitOfWork.Bids.GetBidsByAuctionIdAsync(auction.Id, top: 100, cancellationToken);
        var runnerUpBid = allBids.FirstOrDefault(b => b.ParticipantId == participant.Id);

        // Lấy mức giá của Người về nhì từ RunnerUpFinalPrice (mức giá Bid cuối của chính họ, không ép theo giá người thắng)
        decimal priceToPay;
        if (auctionResult.RunnerUpFinalPrice.HasValue && auctionResult.RunnerUpFinalPrice > 0)
        {
            priceToPay = auctionResult.RunnerUpFinalPrice.Value;
        }
        else
        {
            if (runnerUpBid == null)
            {
                throw new BadRequestException("Không tìm thấy lịch sử trả giá của Người về nhì.");
            }
            priceToPay = runnerUpBid.BidAmount;
        }

        if (wallet.AvailableBalance < priceToPay)
        {
            throw new BadRequestException(
                $"Số dư khả dụng ({wallet.AvailableBalance:N0} VNĐ) không đủ để thanh toán theo giá bid của bạn ({priceToPay:N0} VNĐ). Vui lòng nạp thêm tiền vào ví.");
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
            EscrowDeadline = null,
            SigningDeadline = signingDeadline,
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
            EscrowAmount = priceToPay,
            EscrowStatus = EscrowStatus.HELD,
            HeldAt = now
        };

        // Ghi nhận chứng từ sổ cái kép ESCROW_HOLD (Do Runner-up đã được hoàn cọc lúc hết phiên, họ thanh toán toàn bộ priceToPay: Nợ 2010 / Có 2030)
        var journal = await _ledgerService.PostEscrowHoldAsync(
            wallet.Id,
            priceToPay,
            existingDepositAmount: 0m,
            escrowId,
            cancellationToken);

        // Tạo bản ghi AuctionTransaction
        var auctionTx = new AuctionTransaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            AuctionId = auction.Id,
            EscrowId = escrowId,
            AuctionTransactionType = AuctionTransactionType.ESCROW_HOLD,
            TransactionStatus = TransactionStatus.COMPLETED,
            Amount = priceToPay,
            BalanceBefore = wallet.AvailableBalance + priceToPay,
            BalanceAfter = wallet.AvailableBalance,
            LedgerJournalId = journal.Id,
            JournalCode = journal.JournalCode,
            CreatedAt = now,
            CompletedAt = now
        };

        // Gán quan hệ 2 chiều
        trade.Escrow = escrow;

        // Hủy deadline Á quân trong Delay Queue và nạp deadline ký hợp đồng
        await _delayQueue.CancelJobAsync(auction.Id, AuctionDelayJobTypes.RUNNER_UP_DEADLINE, cancellationToken);
        await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
        {
            AuctionId = auction.Id,
            JobType = AuctionDelayJobTypes.SIGNING_DEADLINE,
            ExecuteAt = trade.SigningDeadline!.Value,
            ExtraData = trade.Id.ToString()
        }, cancellationToken);

        // Cập nhật trạng thái SIM sang TRANSFERRING
        var sim = await _unitOfWork.SimNumbers.GetByIdAsync(auction.SimNumberId, cancellationToken);
        if (sim != null)
        {
            sim.SimStatus = SimStatus.TRANSFERRING;
            _unitOfWork.SimNumbers.Update(sim);
        }

        // Cập nhật Bid của Runner-Up thành WINNING
        if (runnerUpBid != null)
        {
            runnerUpBid.Status = BidStatus.WINNING;
            _unitOfWork.Bids.Update(runnerUpBid);
        }

        // Cập nhật giá hiện tại của Auction thành giá mua thực tế của Runner-Up
        auction.CurrentPrice = priceToPay;
        _unitOfWork.Auctions.Update(auction);

        // Đảm bảo ví giữ trạng thái ACTIVE
        wallet.WalletStatus = WalletStatus.ACTIVE;
        _unitOfWork.Wallets.Update(wallet);

        participant.Status = ParticipantStatus.WON;
        _unitOfWork.Auctions.UpdateParticipant(participant);

        await _unitOfWork.Escrows.AddAsync(escrow, cancellationToken);
        await _unitOfWork.TradeTransactions.AddAsync(trade, cancellationToken);
        await _unitOfWork.AuctionTransactions.AddAsync(auctionTx, cancellationToken);

        auctionResult.WinnerId = participant.Id;
        auctionResult.FinalPrice = priceToPay;
        auctionResult.WinType = AuctionWinType.RUNNER_UP_TAKEOVER;
        auctionResult.AuctionResultStatus = AuctionResultStatus.CONFIRMED;
        auctionResult.SettledAt = now;
        _unitOfWork.Auctions.UpdateResult(auctionResult);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Tự động sinh Văn bản 1 (Phiếu chốt giá trúng) và tải lên Supabase Storage CDN ngay lập tức
        string? winningNoticeUrl = null;
        try
        {
            winningNoticeUrl = await _pdfContractService.EnsureWinningNoticeUploadedAsync(trade.Id, cancellationToken);
        }
        catch
        {
            // fallback nếu lỗi
        }

        // Gửi thông báo cho Á quân và Seller
        var graceHours = auction.AuctionRule?.SigningGracePeriodHours ?? 24;

        await _notificationService.SendNotificationAsync(
            userId: participant.UserAccountId,
            type: NotificationType.CONTRACT_SIGNING_REQUIRED,
            title: $"📝 Yêu cầu ký hợp đồng SIM {sim?.PhoneNumber}!",
            content: $"Bạn đã mua thành công SIM theo đề nghị Á quân phiên {auction.AuctionCode}. Tiền đã được ký quỹ Escrow an toàn. Vui lòng xem biên bản chốt giá trúng và ký hợp đồng điện tử trong vòng {graceHours} giờ.",
            actionUrl: winningNoticeUrl ?? $"/trades/{trade.Id}",
            referenceType: "TradeTransaction",
            referenceId: trade.Id,
            sendEmail: true,
            cancellationToken: cancellationToken);

        await _notificationService.SendNotificationAsync(
            userId: auction.AuthorId,
            type: NotificationType.CONTRACT_SIGNING_REQUIRED,
            title: $"📝 Á quân đã mua SIM {sim?.PhoneNumber}! Vui lòng ký hợp đồng",
            content: $"Á quân phiên {auction.AuctionCode} đã thanh toán đủ {priceToPay:N0} VNĐ vào quỹ Escrow. Vui lòng xem biên bản chốt giá trúng và ký hợp đồng điện tử trong vòng {graceHours} giờ.",
            actionUrl: winningNoticeUrl ?? $"/trades/{trade.Id}",
            referenceType: "TradeTransaction",
            referenceId: trade.Id,
            sendEmail: true,
            cancellationToken: cancellationToken);

        var resultDto = new AuctionResultDto
        {
            Id = auctionResult.Id,
            AuctionId = auction.Id,
            WinnerId = participant.Id,
            WinnerUserName = participant.UserAccount?.UserName,
            FinalPrice = priceToPay,
            InitialWinnerId = auctionResult.InitialWinnerId,
            InitialWinningPrice = auctionResult.InitialWinningPrice,
            RunnerUpParticipantId = auctionResult.RunnerUpParticipantId,
            RunnerUpFinalPrice = auctionResult.RunnerUpFinalPrice,
            WinType = AuctionWinType.RUNNER_UP_TAKEOVER,
            AuctionResultStatus = AuctionResultStatus.CONFIRMED,
            SettledAt = now
        };

        await _realtimeNotifier.NotifyAuctionStatusChangedAsync(
            auction.Id,
            AuctionResultStatus.CONFIRMED.ToString(),
            participant.Id,
            priceToPay,
            cancellationToken);

        return resultDto;
    }
}
