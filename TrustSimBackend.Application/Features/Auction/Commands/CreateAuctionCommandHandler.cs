using MediatR;
using Microsoft.Extensions.Options;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;
using TrustSimBackend.Application.Features.Auction.Factory;
using TrustSimBackend.Application.Features.Auction.Hubs;
using TrustSimBackend.Application.Features.Auction.Services;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Commands;

public class CreateAuctionCommandHandler : IRequestHandler<CreateAuctionCommand, AuctionDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuctionFactory _auctionFactory;
    private readonly IAuctionRealtimeNotifier _realtimeNotifier;
    private readonly IAuctionDelayQueueService _delayQueue;
    private readonly ILedgerService _ledgerService;
    private readonly SellerDepositSettings _depositSettings;

    public CreateAuctionCommandHandler(
        IUnitOfWork unitOfWork,
        AuctionFactory auctionFactory,
        IAuctionRealtimeNotifier realtimeNotifier,
        IAuctionDelayQueueService delayQueue,
        ILedgerService ledgerService,
        IOptions<SellerDepositSettings> depositOptions)
    {
        _unitOfWork = unitOfWork;
        _auctionFactory = auctionFactory;
        _realtimeNotifier = realtimeNotifier;
        _delayQueue = delayQueue;
        _ledgerService = ledgerService;
        _depositSettings = depositOptions.Value;
    }

    public async Task<AuctionDto> Handle(CreateAuctionCommand request, CancellationToken cancellationToken)
    {
        var sim = await _unitOfWork.SimNumbers.GetByIdAsync(request.SimNumberId, cancellationToken);
        if (sim == null)
        {
            throw new NotFoundException($"Không tìm thấy số SIM với ID '{request.SimNumberId}'.");
        }

        if (sim.SimStatus != SimStatus.AVAILABLE)
        {
            throw new BadRequestException("Số SIM này không ở trạng thái sẵn sàng để tạo phiên đấu giá.");
        }

        // Kiểm tra và tính toán tiền cọc Người Bán (Seller Listing Deposit) tại Server: Clamp(StartingPrice * Rate, Min, Max)
        var depositRate = _depositSettings.DepositRate;
        var rawDeposit = Math.Round(request.StartingPrice * depositRate);
        var sellerDeposit = Math.Clamp(rawDeposit, _depositSettings.MinDepositAmount, _depositSettings.MaxDepositAmount);

        var sellerWallet = await _unitOfWork.Wallets.GetByUserIdAsync(request.AuthorId, cancellationToken);
        if (sellerWallet == null)
        {
            throw new NotFoundException("Không tìm thấy ví tiền của người bán.");
        }

        if (sellerWallet.AvailableBalance < sellerDeposit)
        {
            throw new BadRequestException(
                $"Số dư khả dụng trong ví ({sellerWallet.AvailableBalance:N0} VNĐ) không đủ để tạm khóa cọc cam kết bán ({sellerDeposit:N0} VNĐ). Vui lòng nạp thêm tiền vào ví.");
        }

        var auctionCode = $"AUC-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
        while (await _unitOfWork.Auctions.ExistsByCodeAsync(auctionCode, cancellationToken))
        {
            auctionCode = $"AUC-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
        }

        var auction = _auctionFactory.Create(request, auctionCode, request.AuthorId);
        if (auction.AuctionRule != null)
        {
            auction.AuctionRule.SellerDepositAmount = sellerDeposit;
        }

        sim.SimStatus = SimStatus.IN_AUCTION;

        // Khóa cọc Seller vào Sổ cái kép
        await _ledgerService.PostSellerListingDepositLockAsync(sellerWallet.Id, sellerDeposit, auction.Id, cancellationToken);
        sellerWallet.AvailableBalance -= sellerDeposit;
        sellerWallet.LockedBalance += sellerDeposit;
        _unitOfWork.Wallets.Update(sellerWallet);

        await _unitOfWork.Auctions.AddAsync(auction, cancellationToken);
        _unitOfWork.SimNumbers.Update(sim);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Đẩy tác vụ kích hoạt phiên vào Redis ZSET Delay Queue
        await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
        {
            AuctionId = auction.Id,
            JobType = AuctionDelayJobTypes.AUCTION_START,
            ExecuteAt = auction.StartAt
        }, cancellationToken);

        var dto = new AuctionDto
        {
            Id = auction.Id,
            AuctionCode = auction.AuctionCode,
            AuctionType = auction.AuctionType,
            AuctionStatus = auction.AuctionStatus,
            StartingPrice = auction.StartingPrice,
            CurrentPrice = auction.CurrentPrice,
            ReservePrice = auction.ReservePrice,
            StartAt = auction.StartAt,
            EndAt = auction.EndAt,
            RegistrationStartAt = auction.RegistrationStartAt,
            RegistrationEndAt = auction.RegistrationEndAt,
            AuthorId = auction.AuthorId,
            SimNumberId = auction.SimNumberId,
            SimPhoneNumber = sim.PhoneNumber,
            CarrierName = sim.PhonePrefix?.SimCarrier?.CarrierName ?? string.Empty
        };

        // Bắn sự kiện realtime tới trang danh sách /auctions
        await _realtimeNotifier.NotifyNewAuctionCreatedAsync(dto, cancellationToken);

        return dto;
    }
}
