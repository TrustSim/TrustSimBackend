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

    public AcceptRunnerUpOfferCommandHandler(
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

        // Lấy mức giá của Người về nhì từ RunnerUpFinalPrice (mức giá Bid cuối của chính họ, không ép theo giá người thắng)
        decimal priceToPay;
        if (auctionResult.RunnerUpFinalPrice.HasValue && auctionResult.RunnerUpFinalPrice > 0)
        {
            priceToPay = auctionResult.RunnerUpFinalPrice.Value;
        }
        else
        {
            var bids = await _unitOfWork.Bids.GetBidsByAuctionIdAsync(auction.Id, top: 100, cancellationToken);
            var runnerUpBid = bids.FirstOrDefault(b => b.ParticipantId == participant.Id);
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

        // Hủy deadline trong Delay Queue
        await _delayQueue.CancelJobAsync(auction.Id, AuctionDelayJobTypes.RUNNER_UP_DEADLINE, cancellationToken);

        participant.Status = ParticipantStatus.WON;
        _unitOfWork.Auctions.UpdateParticipant(participant);

        await _unitOfWork.TradeTransactions.AddAsync(trade, cancellationToken);

        auctionResult.WinnerId = participant.Id;
        auctionResult.FinalPrice = priceToPay;
        auctionResult.AuctionResultStatus = AuctionResultStatus.CONFIRMED;
        auctionResult.SettledAt = now;
        _unitOfWork.Auctions.UpdateResult(auctionResult);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var resultDto = new AuctionResultDto
        {
            AuctionId = auction.Id,
            WinnerId = participant.Id,
            FinalPrice = priceToPay,
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
