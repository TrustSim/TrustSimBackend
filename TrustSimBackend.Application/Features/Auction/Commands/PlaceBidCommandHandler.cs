using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;
using TrustSimBackend.Application.Features.Auction.Hubs;
using TrustSimBackend.Application.Features.Auction.Services;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Commands;

public class PlaceBidCommandHandler : IRequestHandler<PlaceBidCommand, BidDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuctionRealtimeNotifier _realtimeNotifier;
    private readonly IAuctionDelayQueueService _delayQueue;

    public PlaceBidCommandHandler(
        IUnitOfWork unitOfWork,
        IAuctionRealtimeNotifier realtimeNotifier,
        IAuctionDelayQueueService delayQueue)
    {
        _unitOfWork = unitOfWork;
        _realtimeNotifier = realtimeNotifier;
        _delayQueue = delayQueue;
    }

    public async Task<BidDto> Handle(PlaceBidCommand request, CancellationToken cancellationToken)
    {
        var auction = await _unitOfWork.Auctions.GetByIdAsync(request.AuctionId, cancellationToken);
        if (auction == null)
        {
            throw new NotFoundException($"Không tìm thấy phiên đấu giá với ID '{request.AuctionId}'.");
        }

        if (auction.AuctionType != AuctionType.ENGLISH)
        {
            throw new BadRequestException("Phiên đấu giá này không phải là loại hình đấu giá tăng dần (English Auction).");
        }

        if (auction.AuctionStatus != AuctionStatus.ACTIVE)
        {
            throw new BadRequestException("Phiên đấu giá chưa mở hoặc đã kết thúc.");
        }

        if (auction.AuthorId == request.UserId)
        {
            throw new BadRequestException("Người tạo phiên đấu giá không được phép đặt giá trong phiên của mình.");
        }

        var participant = await _unitOfWork.Auctions.GetParticipantAsync(request.AuctionId, request.UserId, cancellationToken);
        if (participant == null)
        {
            throw new BadRequestException("Bạn chưa đăng ký tham gia phiên đấu giá này.");
        }

        if (participant.Status == ParticipantStatus.BANNED)
        {
            throw new ForbiddenAccessException("Tài khoản của bạn đã bị khóa quyền tham gia trả giá trong phiên này.");
        }

        var rule = auction.AuctionRule;
        var minIncrement = rule?.MinimumBidIncrement ?? 100_000m;
        var minRequiredPrice = auction.CurrentPrice + minIncrement;

        if (request.BidAmount < minRequiredPrice)
        {
            throw new BadRequestException($"Giá trả ({request.BidAmount:N0} VNĐ) phải cao hơn giá hiện tại ít nhất bước giá tối thiểu ({minIncrement:N0} VNĐ). Mức giá tối thiểu là {minRequiredPrice:N0} VNĐ.");
        }

        // Kiểm tra trần bước giá tối đa N lần bước giá (chống nhập giá ảo phá hoại)
        var maxMultiplier = rule?.MaxStepMultiplier > 0 ? rule.MaxStepMultiplier : 10;
        var maxAllowedPrice = auction.CurrentPrice + (maxMultiplier * minIncrement);
        if (request.BidAmount > maxAllowedPrice)
        {
            throw new BadRequestException($"Giá trả ({request.BidAmount:N0} VNĐ) vượt quá mức tăng tối đa cho phép ({maxAllowedPrice:N0} VNĐ, tối đa {maxMultiplier} lần bước giá).");
        }

        // Quy tắc No Self-Outbid: Người đang giữ giá cao nhất không được tự vượt giá của mình
        var highestBid = await _unitOfWork.Bids.GetHighestBidAsync(auction.Id, cancellationToken);
        if (highestBid != null && highestBid.ParticipantId == participant.Id)
        {
            throw new BadRequestException("Bạn đang là người giữ mức giá cao nhất hiện tại. Vui lòng đợi người tham gia khác trả giá trước khi đặt tiếp.");
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

        if (highestBid != null)
        {
            highestBid.Status = BidStatus.OUTBIB;
        }

        auction.CurrentPrice = request.BidAmount;
        auction.LockVersion++;
        participant.AcceptedBidCount++;

        // Kiểm tra cơ chế Anti-Sniping (gia hạn giờ chót)
        bool wasExtended = false;
        int extensionSeconds = 30;
        if (rule != null && rule.ExtensionThresholdSeconds > 0)
        {
            var remainingSeconds = (auction.EndAt - DateTime.UtcNow).TotalSeconds;
            var maxSnipeK = rule.MaxSnipeExtensionsPerParticipant > 0 ? rule.MaxSnipeExtensionsPerParticipant : 3;

            if (remainingSeconds > 0 && remainingSeconds <= rule.ExtensionThresholdSeconds && participant.SnipeExtensionCount < maxSnipeK)
            {
                extensionSeconds = (int)(rule.ExtensionDurationSeconds > 0 ? rule.ExtensionDurationSeconds : 30);
                auction.EndAt = auction.EndAt.AddSeconds(extensionSeconds);
                participant.SnipeExtensionCount++;
                wasExtended = true;

                // Lập lại lịch kết thúc trong Redis Delay Queue
                await _delayQueue.CancelJobAsync(auction.Id, AuctionDelayJobTypes.AUCTION_END, cancellationToken);
                await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
                {
                    AuctionId = auction.Id,
                    JobType = AuctionDelayJobTypes.AUCTION_END,
                    ExecuteAt = auction.EndAt
                }, cancellationToken);
            }
        }

        await _unitOfWork.Bids.AddAsync(bid, cancellationToken);
        _unitOfWork.Auctions.Update(auction);
        _unitOfWork.Auctions.UpdateParticipant(participant);
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
