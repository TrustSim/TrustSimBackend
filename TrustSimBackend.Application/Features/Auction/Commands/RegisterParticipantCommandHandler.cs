using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Commands;

public class RegisterParticipantCommandHandler : IRequestHandler<RegisterParticipantCommand, bool>
{
    private readonly IAuctionRepository _auctionRepository;
    private readonly IWalletRepository _walletRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterParticipantCommandHandler(
        IAuctionRepository auctionRepository,
        IWalletRepository walletRepository,
        IUnitOfWork unitOfWork)
    {
        _auctionRepository = auctionRepository;
        _walletRepository = walletRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(RegisterParticipantCommand request, CancellationToken cancellationToken)
    {
        var auction = await _auctionRepository.GetByIdAsync(request.AuctionId, cancellationToken);
        if (auction == null)
        {
            throw new NotFoundException($"Không tìm thấy phiên đấu giá với ID '{request.AuctionId}'.");
        }

        var existing = await _auctionRepository.GetParticipantAsync(request.AuctionId, request.UserId, cancellationToken);
        if (existing != null)
        {
            throw new BadRequestException("Bạn đã đăng ký tham gia phiên đấu giá này rồi.");
        }

        var depositRequired = auction.AuctionRule?.DepositAmount ?? 0;
        AuctionTransaction? auctionTransaction = null;

        if (depositRequired > 0)
        {
            var wallet = await _walletRepository.GetByUserIdAsync(request.UserId, cancellationToken);
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
            wallet.AvailableBalance -= depositRequired;
            wallet.LockedBalance += depositRequired;
            var balanceAfter = wallet.AvailableBalance;
            wallet.LockVersion++;

            auctionTransaction = new AuctionTransaction
            {
                Id = Guid.NewGuid(),
                WalletId = wallet.Id,
                AuctionTransactionType = AuctionTransactionType.AUCTION_DEPOSIT_LOCK,
                TransactionStatus = TransactionStatus.COMPLETED,
                Amount = depositRequired,
                BalanceBefore = balanceBefore,
                BalanceAfter = balanceAfter,
                CreatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            };

            wallet.AuctionTransactions.Add(auctionTransaction);
            _walletRepository.Update(wallet);
        }

        var participant = new AuctionParticipant
        {
            Id = Guid.NewGuid(),
            AuctionId = request.AuctionId,
            UserAccountId = request.UserId,
            Status = ParticipantStatus.REGISTERED,
            JoinedAt = DateTime.UtcNow
        };

        if (depositRequired > 0 && auctionTransaction != null)
        {
            var deposit = new AuctionDeposit
            {
                Id = Guid.NewGuid(),
                ParticipantId = participant.Id,
                DepositCode = $"DEP-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
                DepositAmount = depositRequired,
                DepositStatus = DepositStatus.LOCKED,
                AuctionTransactionId = auctionTransaction.Id
            };
            participant.Deposits.Add(deposit);
        }

        await _auctionRepository.AddParticipantAsync(participant, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}

