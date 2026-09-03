using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Auction.DTOs;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Commands;

public record CreateAuctionCommand(
    Guid AuthorId,
    Guid SimNumberId,
    AuctionType AuctionType,
    decimal StartingPrice,
    decimal ReservePrice,
    DateTime StartAt,
    DateTime EndAt,
    DateTime RegistrationStartAt,
    DateTime RegistrationEndAt
) : IRequest<ApiResponse<AuctionDto>>;

public class CreateAuctionCommandHandler : IRequestHandler<CreateAuctionCommand, ApiResponse<AuctionDto>>
{
    private readonly IAuctionRepository _auctionRepository;
    private readonly ISimNumberRepository _simRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAuctionCommandHandler(
        IAuctionRepository auctionRepository,
        ISimNumberRepository simRepository,
        IUnitOfWork unitOfWork)
    {
        _auctionRepository = auctionRepository;
        _simRepository = simRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<AuctionDto>> Handle(CreateAuctionCommand request, CancellationToken cancellationToken)
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

        return ApiResponse<AuctionDto>.SuccessResult(dto, "Tạo phiên đấu giá thành công.");
    }
}

public record RegisterParticipantCommand(
    Guid AuctionId,
    Guid UserId
) : IRequest<ApiResponse<bool>>;

public class RegisterParticipantCommandHandler : IRequestHandler<RegisterParticipantCommand, ApiResponse<bool>>
{
    private readonly IAuctionRepository _auctionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterParticipantCommandHandler(
        IAuctionRepository auctionRepository,
        IUnitOfWork unitOfWork)
    {
        _auctionRepository = auctionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<bool>> Handle(RegisterParticipantCommand request, CancellationToken cancellationToken)
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

        var participant = new AuctionParticipant
        {
            Id = Guid.NewGuid(),
            AuctionId = request.AuctionId,
            UserAccountId = request.UserId,
            Status = ParticipantStatus.REGISTERED,
            JoinedAt = DateTime.UtcNow
        };

        await _auctionRepository.AddParticipantAsync(participant, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.SuccessResult(true, "Đăng ký tham gia phiên đấu giá thành công.");
    }
}

public record PlaceBidCommand(
    Guid AuctionId,
    Guid UserId,
    decimal BidAmount
) : IRequest<ApiResponse<BidDto>>;

public class PlaceBidCommandHandler : IRequestHandler<PlaceBidCommand, ApiResponse<BidDto>>
{
    private readonly IAuctionRepository _auctionRepository;
    private readonly IBidRepository _bidRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PlaceBidCommandHandler(
        IAuctionRepository auctionRepository,
        IBidRepository bidRepository,
        IUnitOfWork unitOfWork)
    {
        _auctionRepository = auctionRepository;
        _bidRepository = bidRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<BidDto>> Handle(PlaceBidCommand request, CancellationToken cancellationToken)
    {
        var auction = await _auctionRepository.GetByIdAsync(request.AuctionId, cancellationToken);
        if (auction == null)
        {
            throw new NotFoundException($"Không tìm thấy phiên đấu giá với ID '{request.AuctionId}'.");
        }

        if (auction.AuctionStatus != AuctionStatus.ACTIVE)
        {
            throw new BadRequestException("Phiên đấu giá chưa mở hoặc đã kết thúc.");
        }

        if (request.BidAmount <= auction.CurrentPrice)
        {
            throw new BadRequestException($"Giá trả ({request.BidAmount:N0} VNĐ) phải cao hơn giá hiện tại ({auction.CurrentPrice:N0} VNĐ).");
        }

        var participant = await _auctionRepository.GetParticipantAsync(request.AuctionId, request.UserId, cancellationToken);
        if (participant == null)
        {
            throw new BadRequestException("Bạn chưa đăng ký tham gia phiên đấu giá này.");
        }

        var bid = new Bid
        {
            Id = Guid.NewGuid(),
            AuctionId = request.AuctionId,
            ParticipantId = participant.Id,
            BidAmount = request.BidAmount,
            PlacedAt = DateTime.UtcNow,
            Status = BidStatus.ACCEPTED
        };

        auction.CurrentPrice = request.BidAmount;
        auction.LockVersion++;

        await _bidRepository.AddAsync(bid, cancellationToken);
        _auctionRepository.Update(auction);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = new BidDto
        {
            Id = bid.Id,
            AuctionId = bid.AuctionId,
            ParticipantId = bid.ParticipantId,
            UserAccountId = participant.UserAccountId,
            UserNameMasked = "***" + request.UserId.ToString()[^4..],
            BidAmount = bid.BidAmount,
            PlacedAt = bid.PlacedAt,
            Status = bid.Status
        };

        return ApiResponse<BidDto>.SuccessResult(dto, "Trả giá thành công.");
    }
}
