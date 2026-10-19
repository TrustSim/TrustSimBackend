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
/// Handler xử lý LockAuctionDepositCommand (Khóa cọc tham gia đấu giá -> Nợ 2010 / Có 2020).
/// </summary>
public class LockAuctionDepositCommandHandler : IRequestHandler<LockAuctionDepositCommand, LedgerJournalDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerService _ledgerService;
    private readonly ILogger<LockAuctionDepositCommandHandler> _logger;

    public LockAuctionDepositCommandHandler(
        IUnitOfWork unitOfWork,
        ILedgerService ledgerService,
        ILogger<LockAuctionDepositCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _ledgerService = ledgerService;
        _logger = logger;
    }

    public async Task<LedgerJournalDto> Handle(LockAuctionDepositCommand request, CancellationToken cancellationToken)
    {
        if (request.AmountToLock <= 0)
        {
            throw new BadRequestException("Số tiền đặt cọc đấu giá phải lớn hơn 0.");
        }

        var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(request.UserId, cancellationToken);
        if (wallet == null)
        {
            throw new NotFoundException($"Không tìm thấy ví tiền của người dùng ID '{request.UserId}'.");
        }

        if (wallet.WalletStatus != WalletStatus.ACTIVE)
        {
            throw new BadRequestException("Ví tiền đang bị khóa hoặc đóng băng, không thể thực hiện đặt cọc.");
        }

        if (wallet.AvailableBalance < request.AmountToLock)
        {
            throw new BadRequestException(
                $"Số dư khả dụng ({wallet.AvailableBalance:N0} VNĐ) không đủ để đặt cọc {request.AmountToLock:N0} VNĐ cho đấu giá.");
        }

        var depositId = request.AuctionDepositId ?? Guid.NewGuid();

        // Ghi sổ cái kế toán ghi kép: Nợ 2010 USER_AVAILABLE / Có 2020 USER_LOCKED
        var journal = await _ledgerService.PostAuctionDepositLockAsync(
            wallet.Id,
            request.AmountToLock,
            depositId,
            cancellationToken);

        var now = DateTime.UtcNow;
        var lockTx = new AuctionTransaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            AuctionId = request.AuctionId,
            AuctionTransactionType = AuctionTransactionType.AUCTION_DEPOSIT_LOCK,
            TransactionStatus = TransactionStatus.COMPLETED,
            Amount = request.AmountToLock,
            BalanceBefore = wallet.AvailableBalance + request.AmountToLock,
            BalanceAfter = wallet.AvailableBalance,
            LedgerJournalId = journal.Id,
            JournalCode = journal.JournalCode,
            CreatedAt = now,
            CompletedAt = now
        };
        await _unitOfWork.AuctionTransactions.AddAsync(lockTx, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[Auction Deposit] Đã khóa cọc {Amount:N0} VNĐ cho UserId='{UserId}', AuctionId='{AuctionId}' (Chứng từ #{JournalCode})",
            request.AmountToLock, request.UserId, request.AuctionId, journal.JournalCode);

        return LedgerJournalDto.FromEntity(journal);
    }
}
