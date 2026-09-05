using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Commands;

public class PlaceBidCommandHandler : IRequestHandler<PlaceBidCommand, BidDto>
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

    public async Task<BidDto> Handle(PlaceBidCommand request, CancellationToken cancellationToken)
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

        return new BidDto
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
    }
}
