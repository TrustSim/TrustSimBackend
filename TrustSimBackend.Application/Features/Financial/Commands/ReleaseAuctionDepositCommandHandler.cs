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
/// Handler xử lý ReleaseAuctionDepositCommand (Hoàn cọc không trúng đấu giá -> Nợ 2020 / Có 2010).
/// </summary>
public class ReleaseAuctionDepositCommandHandler : IRequestHandler<ReleaseAuctionDepositCommand, LedgerJournalDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerService _ledgerService;
    private readonly ILogger<ReleaseAuctionDepositCommandHandler> _logger;

    public ReleaseAuctionDepositCommandHandler(
        IUnitOfWork unitOfWork,
        ILedgerService ledgerService,
        ILogger<ReleaseAuctionDepositCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _ledgerService = ledgerService;
        _logger = logger;
    }

    public async Task<LedgerJournalDto> Handle(ReleaseAuctionDepositCommand request, CancellationToken cancellationToken)
    {
        if (request.AmountToRelease <= 0)
        {
            throw new BadRequestException("Số tiền hoàn cọc đấu giá phải lớn hơn 0.");
        }

        var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(request.UserId, cancellationToken);
        if (wallet == null)
        {
            throw new NotFoundException($"Không tìm thấy ví tiền của người dùng ID '{request.UserId}'.");
        }

        if (wallet.LockedBalance < request.AmountToRelease)
        {
            throw new BadRequestException(
                $"Số dư phong tỏa ({wallet.LockedBalance:N0} VNĐ) không đủ để hoàn {request.AmountToRelease:N0} VNĐ.");
        }

        var depositId = request.AuctionDepositId ?? Guid.NewGuid();

        // Ghi sổ cái kế toán ghi kép: Nợ 2020 USER_LOCKED / Có 2010 USER_AVAILABLE
        var journal = await _ledgerService.PostAuctionDepositReleaseAsync(
            wallet.Id,
            request.AmountToRelease,
            depositId,
            cancellationToken);

        var now = DateTime.UtcNow;
        var releaseTx = new AuctionTransaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            AuctionId = request.AuctionId,
            AuctionTransactionType = AuctionTransactionType.AUCTION_DEPOSIT_RELEASED,
            TransactionStatus = TransactionStatus.COMPLETED,
            Amount = request.AmountToRelease,
            BalanceBefore = wallet.AvailableBalance - request.AmountToRelease,
            BalanceAfter = wallet.AvailableBalance,
            LedgerJournalId = journal.Id,
            JournalCode = journal.JournalCode,
            CreatedAt = now,
            CompletedAt = now
        };
        await _unitOfWork.AuctionTransactions.AddAsync(releaseTx, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[Auction Release Deposit] Đã hoàn cọc {Amount:N0} VNĐ cho UserId='{UserId}', AuctionId='{AuctionId}' (Chứng từ #{JournalCode})",
            request.AmountToRelease, request.UserId, request.AuctionId, journal.JournalCode);

        return LedgerJournalDto.FromEntity(journal);
    }
}
