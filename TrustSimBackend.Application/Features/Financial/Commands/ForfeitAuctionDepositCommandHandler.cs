using MediatR;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Financial.Commands;

/// <summary>
/// Handler xử lý ForfeitAuctionDepositCommand (Tịch thu cọc vi phạm -> Nợ 2020 / Có 4020).
/// </summary>
public class ForfeitAuctionDepositCommandHandler : IRequestHandler<ForfeitAuctionDepositCommand, LedgerJournalDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerService _ledgerService;
    private readonly ILogger<ForfeitAuctionDepositCommandHandler> _logger;

    public ForfeitAuctionDepositCommandHandler(
        IUnitOfWork unitOfWork,
        ILedgerService ledgerService,
        ILogger<ForfeitAuctionDepositCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _ledgerService = ledgerService;
        _logger = logger;
    }

    public async Task<LedgerJournalDto> Handle(ForfeitAuctionDepositCommand request, CancellationToken cancellationToken)
    {
        if (request.AmountToForfeit <= 0)
        {
            throw new BadRequestException("Số tiền tịch thu cọc phải lớn hơn 0.");
        }

        var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(request.UserId, cancellationToken);
        if (wallet == null)
        {
            throw new NotFoundException($"Không tìm thấy ví tiền của người dùng ID '{request.UserId}'.");
        }

        if (wallet.LockedBalance < request.AmountToForfeit)
        {
            throw new BadRequestException(
                $"Số dư phong tỏa ({wallet.LockedBalance:N0} VNĐ) không đủ để tịch thu {request.AmountToForfeit:N0} VNĐ.");
        }

        var depositId = request.AuctionDepositId ?? Guid.NewGuid();

        // Ghi sổ cái kế toán ghi kép: Nợ 2020 USER_LOCKED / Có 4020 PLATFORM_PENALTY_REVENUE
        var journal = await _ledgerService.PostAuctionDepositForfeitedAsync(
            wallet.Id,
            request.AmountToForfeit,
            depositId,
            cancellationToken);

        var now = DateTime.UtcNow;
        var forfeitTx = new AuctionTransaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            AuctionId = request.AuctionId,
            AuctionTransactionType = AuctionTransactionType.AUCTION_DEPOSIT_FORFEITED,
            TransactionStatus = TransactionStatus.COMPLETED,
            Amount = request.AmountToForfeit,
            BalanceBefore = wallet.AvailableBalance,
            BalanceAfter = wallet.AvailableBalance,
            LedgerJournalId = journal.Id,
            JournalCode = journal.JournalCode,
            CreatedAt = now,
            CompletedAt = now
        };
        await _unitOfWork.AuctionTransactions.AddAsync(forfeitTx, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "[Auction Forfeit Deposit] Đã tịch thu cọc {Amount:N0} VNĐ từ UserId='{UserId}', AuctionId='{AuctionId}', Lý do='{Reason}' (Chứng từ #{JournalCode})",
            request.AmountToForfeit, request.UserId, request.AuctionId, request.ViolationReason, journal.JournalCode);

        return LedgerJournalDto.FromEntity(journal);
    }
}
