using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;
using TrustSimBackend.Application.Features.Auction.Factory;
using TrustSimBackend.Application.Features.Auction.Hubs;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

using TrustSimBackend.Application.Features.Auction.Services;

namespace TrustSimBackend.Application.Features.Auction.Commands;

public class CreateAuctionCommandHandler : IRequestHandler<CreateAuctionCommand, AuctionDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuctionFactory _auctionFactory;
    private readonly IAuctionRealtimeNotifier _realtimeNotifier;
    private readonly IAuctionDelayQueueService _delayQueue;

    public CreateAuctionCommandHandler(
        IUnitOfWork unitOfWork,
        AuctionFactory auctionFactory,
        IAuctionRealtimeNotifier realtimeNotifier,
        IAuctionDelayQueueService delayQueue)
    {
        _unitOfWork = unitOfWork;
        _auctionFactory = auctionFactory;
        _realtimeNotifier = realtimeNotifier;
        _delayQueue = delayQueue;
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

        var auctionCode = $"AUC-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
        while (await _unitOfWork.Auctions.ExistsByCodeAsync(auctionCode, cancellationToken))
        {
            auctionCode = $"AUC-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
        }

        var auction = _auctionFactory.Create(request, auctionCode, request.AuthorId);
        sim.SimStatus = SimStatus.IN_AUCTION;

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
