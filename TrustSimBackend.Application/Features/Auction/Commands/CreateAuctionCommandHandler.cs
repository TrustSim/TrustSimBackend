using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;
using TrustSimBackend.Application.Features.Auction.Hubs;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Commands;

public class CreateAuctionCommandHandler : IRequestHandler<CreateAuctionCommand, AuctionDto>
{
    private readonly IAuctionRepository _auctionRepository;
    private readonly ISimNumberRepository _simRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuctionRealtimeNotifier _realtimeNotifier;

    public CreateAuctionCommandHandler(
        IAuctionRepository auctionRepository,
        ISimNumberRepository simRepository,
        IUnitOfWork unitOfWork,
        IAuctionRealtimeNotifier realtimeNotifier)
    {
        _auctionRepository = auctionRepository;
        _simRepository = simRepository;
        _unitOfWork = unitOfWork;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<AuctionDto> Handle(CreateAuctionCommand request, CancellationToken cancellationToken)
    {
        var sim = await _simRepository.GetByIdAsync(request.SimNumberId, cancellationToken);
        if (sim == null)
        {
            throw new NotFoundException($"Không tìm thấy số SIM với ID '{request.SimNumberId}'.");
        }

        if (sim.SimStatus != SimStatus.AVAILABLE)
        {
            throw new BadRequestException("Số SIM này không ở trạng thái sẵn sàng để tạo phiên đấu giá.");
        }

        var auctionCode = $"AUC-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
        while (await _auctionRepository.ExistsByCodeAsync(auctionCode, cancellationToken))
        {
            auctionCode = $"AUC-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
        }

        var auction = new Domain.Entities.Auctions.Auction
        {
            Id = Guid.NewGuid(),
            AuctionCode = auctionCode,
            AuthorId = request.AuthorId,
            SimNumberId = request.SimNumberId,
            AuctionType = request.AuctionType,
            AuctionStatus = AuctionStatus.SCHEDULE,
            StartingPrice = request.StartingPrice,
            CurrentPrice = request.StartingPrice,
            ReservePrice = request.ReservePrice,
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            RegistrationStartAt = request.RegistrationStartAt,
            RegistrationEndAt = request.RegistrationEndAt
        };

        sim.SimStatus = SimStatus.IN_AUCTION;

        await _auctionRepository.AddAsync(auction, cancellationToken);
        _simRepository.Update(sim);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
