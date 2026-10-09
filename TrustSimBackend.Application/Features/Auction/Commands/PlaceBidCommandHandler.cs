using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
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
    private readonly IRedisAuctionCacheService _redisCache;
    private readonly IAuctionRealtimeNotifier _realtimeNotifier;
    private readonly IAuctionDelayQueueService _delayQueue;

    public PlaceBidCommandHandler(
        IUnitOfWork unitOfWork,
        IRedisAuctionCacheService redisCache,
        IAuctionRealtimeNotifier realtimeNotifier,
        IAuctionDelayQueueService delayQueue)
    {
        _unitOfWork = unitOfWork;
        _redisCache = redisCache;
        _realtimeNotifier = realtimeNotifier;
        _delayQueue = delayQueue;
    }

    public async Task<BidDto> Handle(PlaceBidCommand request, CancellationToken cancellationToken)
    {
        var participant = await _unitOfWork.Auctions.GetParticipantAsync(request.AuctionId, request.UserId, cancellationToken);
        if (participant == null)
            throw new BadRequestException("Bạn chưa đăng ký tham gia phiên đấu giá này.");

        if (participant.Status == ParticipantStatus.BANNED)
            throw new ForbiddenAccessException("Tài khoản của bạn đã bị khóa quyền tham gia trả giá.");

        var bidId = Guid.NewGuid();

        // 1. THỰC THI QUA REDIS ATOMIC LUA SCRIPT ENGINE (<1ms)
        var luaResult = await _redisCache.PlaceBidAtomicAsync(
            request.AuctionId,
            participant.Id,
            request.UserId,
            request.BidAmount,
            bidId,
            cancellationToken);

        if (!luaResult.Success && luaResult.ErrorCode != "ERR_REDIS_UNAVAILABLE")
        {
            throw new BadRequestException(luaResult.Message ?? "Lượt đặt giá không hợp lệ.");
        }

        // 2. NẾU REDIS THÀNH CÔNG: BROADCAST VÀ RETURN NGAY
        if (luaResult.Success)
        {
            var newEndAt = DateTimeOffset.FromUnixTimeMilliseconds(luaResult.NewEndAtMs).UtcDateTime;

            if (luaResult.WasExtended)
            {
                await _delayQueue.CancelJobAsync(request.AuctionId, AuctionDelayJobTypes.AUCTION_END, cancellationToken);
                await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
                {
                    AuctionId = request.AuctionId,
                    JobType = AuctionDelayJobTypes.AUCTION_END,
                    ExecuteAt = newEndAt
                }, cancellationToken);
            }

            var maskedName = participant.UserAccount?.UserName != null && participant.UserAccount.UserName.Length > 3
                ? participant.UserAccount.UserName[..2] + "***" + participant.UserAccount.UserName[^1..]
                : ("***" + request.UserId.ToString()[^4..]);

            var bidDto = new BidDto
            {
                Id = bidId,
                AuctionId = request.AuctionId,
                ParticipantId = participant.Id,
                UserAccountId = participant.UserAccountId,
                UserNameMasked = maskedName,
                BidAmount = luaResult.BidAmount,
                PlacedAt = DateTime.UtcNow,
                Status = BidStatus.ACCEPTED
            };

            // Bắn SignalR Broadcast Realtime
            await _realtimeNotifier.NotifyNewBidAsync(
                request.AuctionId,
                bidDto,
                luaResult.NewCurrentPrice,
                (int)luaResult.LockVersion,
                cancellationToken);

            if (luaResult.WasExtended)
            {
                await _realtimeNotifier.NotifyAuctionExtendedAsync(
                    request.AuctionId,
                    newEndAt,
                    30,
                    cancellationToken);
            }

            return bidDto;
        }

        // 3. FALLBACK CƠ SỞ DỮ LIỆU SQL SERVER (KHI REDIS OFFLINE)
        var auction = await _unitOfWork.Auctions.GetByIdAsync(request.AuctionId, cancellationToken);
        if (auction == null || auction.AuctionStatus != AuctionStatus.ACTIVE)
            throw new BadRequestException("Phiên đấu giá chưa mở hoặc đã kết thúc.");

        if (auction.AuthorId == request.UserId)
            throw new BadRequestException("Người tạo phiên đấu giá không được phép đặt giá.");

        var rule = auction.AuctionRule;
        var minIncrement = rule?.MinimumBidIncrement ?? 100_000m;
        var minRequiredPrice = auction.CurrentPrice + minIncrement;

        if (request.BidAmount < minRequiredPrice)
            throw new BadRequestException($"Giá trả ({request.BidAmount:N0} VNĐ) phải tối thiểu là {minRequiredPrice:N0} VNĐ.");

        var maxMultiplier = rule?.MaxStepMultiplier > 0 ? rule.MaxStepMultiplier : 10;
        var maxAllowedPrice = auction.CurrentPrice + (maxMultiplier * minIncrement);
        if (request.BidAmount > maxAllowedPrice)
            throw new BadRequestException($"Giá trả vượt quá mức tăng tối đa cho phép ({maxAllowedPrice:N0} VNĐ).");

        // No Self-Outbid
        var highestBid = await _unitOfWork.Bids.GetHighestBidAsync(auction.Id, cancellationToken);
        if (highestBid != null && highestBid.ParticipantId == participant.Id)
            throw new BadRequestException("Bạn đang là người giữ mức giá cao nhất hiện tại.");

        // Ký quỹ lũy tiến
        var marginRate = rule?.MarginPercentage ?? 0.10m;
        var requiredMargin = request.BidAmount * marginRate;
        var initialDeposit = rule?.DepositAmount ?? 0m;
        var deltaMargin = requiredMargin - initialDeposit;
        if (deltaMargin > 0)
        {
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(request.UserId, cancellationToken);
            if (wallet == null || wallet.AvailableBalance < deltaMargin)
                throw new BadRequestException($"Số dư ví không đủ đáp ứng ký quỹ {requiredMargin:N0} VNĐ (cần thêm {deltaMargin:N0} VNĐ).");
        }

        var fallbackBid = new Bid
        {
            Id = bidId,
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

        bool wasExtendedFallback = false;
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
                wasExtendedFallback = true;

                await _delayQueue.CancelJobAsync(auction.Id, AuctionDelayJobTypes.AUCTION_END, cancellationToken);
                await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
                {
                    AuctionId = auction.Id,
                    JobType = AuctionDelayJobTypes.AUCTION_END,
                    ExecuteAt = auction.EndAt
                }, cancellationToken);
            }
        }

        await _unitOfWork.Bids.AddAsync(fallbackBid, cancellationToken);
        _unitOfWork.Auctions.Update(auction);
        _unitOfWork.Auctions.UpdateParticipant(participant);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var totalBids = await _unitOfWork.Bids.CountByAuctionIdAsync(auction.Id, cancellationToken);

        var fallbackDto = new BidDto
        {
            Id = fallbackBid.Id,
            AuctionId = fallbackBid.AuctionId,
            ParticipantId = fallbackBid.ParticipantId,
            UserAccountId = participant.UserAccountId,
            UserNameMasked = "***" + request.UserId.ToString()[^4..],
            BidAmount = fallbackBid.BidAmount,
            PlacedAt = fallbackBid.PlacedAt,
            Status = fallbackBid.Status
        };

        await _realtimeNotifier.NotifyNewBidAsync(
            auction.Id,
            fallbackDto,
            auction.CurrentPrice,
            totalBids,
            cancellationToken);

        if (wasExtendedFallback)
        {
            await _realtimeNotifier.NotifyAuctionExtendedAsync(
                auction.Id,
                auction.EndAt,
                extensionSeconds,
                cancellationToken);
        }

        return fallbackDto;
    }
}
