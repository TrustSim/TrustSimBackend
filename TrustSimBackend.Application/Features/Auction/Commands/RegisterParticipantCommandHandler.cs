using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Commands;

public class RegisterParticipantCommandHandler : IRequestHandler<RegisterParticipantCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerService _ledgerService;

    public RegisterParticipantCommandHandler(
        IUnitOfWork unitOfWork,
        ILedgerService ledgerService)
    {
        _unitOfWork = unitOfWork;
        _ledgerService = ledgerService;
    }

    public async Task<bool> Handle(RegisterParticipantCommand request, CancellationToken cancellationToken)
    {
        var auction = await _unitOfWork.Auctions.GetByIdAsync(request.AuctionId, cancellationToken);
        if (auction == null)
        {
            throw new NotFoundException($"Không tìm thấy phiên đấu giá với ID '{request.AuctionId}'.");
        }

        if (auction.AuthorId == request.UserId)
        {
            throw new BadRequestException("Người tạo phiên đấu giá không thể đăng ký tham gia trả giá trong phiên của chính mình.");
        }

        if (auction.AuctionStatus != AuctionStatus.SCHEDULE)
        {
            throw new BadRequestException("Chỉ có thể đặt cọc đăng ký tham gia khi phiên đấu giá chưa bắt đầu (trạng thái SCHEDULE). Phiên đang diễn ra hoặc đã kết thúc không cho phép đăng ký mới.");
        }

        var now = DateTime.UtcNow;
        if (now < auction.RegistrationStartAt)
        {
            throw new BadRequestException("Chưa đến thời gian mở cổng đăng ký đặt cọc cho phiên đấu giá này.");
        }

        if (now > auction.RegistrationEndAt || now >= auction.StartAt)
        {
            throw new BadRequestException("Đã hết thời hạn đăng ký đặt cọc tham gia phiên đấu giá này (phiên đã bắt đầu hoặc quá hạn đăng ký).");
        }

        var existing = await _unitOfWork.Auctions.GetParticipantAsync(request.AuctionId, request.UserId, cancellationToken);
        if (existing != null)
        {
            throw new BadRequestException("Bạn đã đăng ký tham gia phiên đấu giá này rồi.");
        }

        // Chống trùng lịch thi đấu: User không được tham gia 2 phiên có khung giờ giao nhau
        var isOverlapping = await _unitOfWork.Auctions.HasOverlappingRegisteredAuctionAsync(
            request.UserId,
            auction.StartAt,
            auction.EndAt,
            excludeAuctionId: auction.Id,
            cancellationToken);

        if (isOverlapping)
        {
            throw new BadRequestException("Bạn đã đăng ký một phiên đấu giá khác có khung thời gian trùng với phiên này. Không thể đăng ký đồng thời.");
        }

        var depositRequired = auction.AuctionRule?.DepositAmount ?? 0;
        AuctionTransaction? auctionTransaction = null;
        AuctionDeposit? deposit = null;

        var participant = new AuctionParticipant
        {
            Id = Guid.NewGuid(),
            AuctionId = request.AuctionId,
            UserAccountId = request.UserId,
            Status = ParticipantStatus.REGISTERED,
            JoinedAt = DateTime.UtcNow
        };

        if (depositRequired > 0)
        {
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(request.UserId, cancellationToken);
            if (wallet == null)
            {
                throw new NotFoundException("Không tìm thấy ví tiền của người dùng. Vui lòng liên hệ bộ phận hỗ trợ.");
            }

            if (wallet.WalletStatus != WalletStatus.ACTIVE)
            {
                throw new BadRequestException("Ví tiền của bạn đang bị khóa hoặc đóng băng. Không thể đặt cọc tham gia đấu giá.");
            }

            if (wallet.AvailableBalance < depositRequired)
            {
                throw new BadRequestException(
                    $"Số dư khả dụng ({wallet.AvailableBalance:N0} VNĐ) không đủ để đặt cọc tham gia ({depositRequired:N0} VNĐ). Vui lòng nạp thêm tiền vào ví.");
            }

            var balanceBefore = wallet.AvailableBalance;
            var depositId = Guid.NewGuid();

            // Ghi nhận chứng từ sổ cái kép AUCTION_DEPOSIT_LOCK (Nợ 2010 USER_AVAILABLE / Có 2020 USER_LOCKED)
            var journal = await _ledgerService.PostAuctionDepositLockAsync(
                wallet.Id,
                depositRequired,
                depositId,
                cancellationToken);

            auctionTransaction = new AuctionTransaction
            {
                Id = Guid.NewGuid(),
                WalletId = wallet.Id,
                AuctionId = auction.Id,
                AuctionTransactionType = AuctionTransactionType.AUCTION_DEPOSIT_LOCK,
                TransactionStatus = TransactionStatus.COMPLETED,
                Amount = depositRequired,
                BalanceBefore = balanceBefore,
                BalanceAfter = wallet.AvailableBalance,
                LedgerJournalId = journal.Id,
                JournalCode = journal.JournalCode,
                CreatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            };

            deposit = new AuctionDeposit
            {
                Id = depositId,
                ParticipantId = participant.Id,
                DepositCode = $"DEP-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
                DepositAmount = depositRequired,
                DepositStatus = DepositStatus.LOCKED,
                AuctionTransactionId = auctionTransaction.Id,
                AuctionTransaction = auctionTransaction
            };

            participant.Deposits.Add(deposit);
        }

        await _unitOfWork.Auctions.AddParticipantAsync(participant, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
