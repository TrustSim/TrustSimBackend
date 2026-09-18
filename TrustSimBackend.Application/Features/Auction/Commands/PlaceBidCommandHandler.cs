using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;
using TrustSimBackend.Application.Features.Auction.Hubs;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Commands;

public class PlaceBidCommandHandler : IRequestHandler<PlaceBidCommand, BidDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuctionRealtimeNotifier _realtimeNotifier;

    public PlaceBidCommandHandler(
        IUnitOfWork unitOfWork,
        IAuctionRealtimeNotifier realtimeNotifier)
    {
        _unitOfWork = unitOfWork;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<BidDto> Handle(PlaceBidCommand request, CancellationToken cancellationToken)
    {
        var auction = await _unitOfWork.Auctions.GetByIdAsync(request.AuctionId, cancellationToken);
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

        var participant = await _unitOfWork.Auctions.GetParticipantAsync(request.AuctionId, request.UserId, cancellationToken);
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

        // Kiểm tra cơ chế Anti-Sniping (gia hạn giờ chót)
        bool wasExtended = false;
        int extensionSeconds = 30;
        if (auction.AuctionRule != null && auction.AuctionRule.ExtensionThresholdSeconds > 0)
        {
            var remainingSeconds = (auction.EndAt - DateTime.UtcNow).TotalSeconds;
            if (remainingSeconds > 0 && remainingSeconds <= auction.AuctionRule.ExtensionThresholdSeconds)
            {
                extensionSeconds = (int)(auction.AuctionRule.ExtensionDurationSeconds > 0
                    ? auction.AuctionRule.ExtensionDurationSeconds
                    : 30);
                auction.EndAt = auction.EndAt.AddSeconds(extensionSeconds);
                wasExtended = true;
            }
        }

        await _unitOfWork.Bids.AddAsync(bid, cancellationToken);
        _unitOfWork.Auctions.Update(auction);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var totalBids = await _unitOfWork.Bids.CountByAuctionIdAsync(auction.Id, cancellationToken);

        var bidDto = new BidDto
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

        // Bắn sự kiện realtime qua SignalR Hub tới tất cả người xem trong phòng
        await _realtimeNotifier.NotifyNewBidAsync(
            auction.Id,
            bidDto,
            auction.CurrentPrice,
            totalBids,
            cancellationToken
        );

        if (wasExtended)
        {
            await _realtimeNotifier.NotifyAuctionExtendedAsync(
                auction.Id,
                auction.EndAt,
                extensionSeconds,
                cancellationToken
            );
        }

        return bidDto;
    }
}
