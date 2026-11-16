using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;
using TrustSimBackend.Application.Features.Auction.Helpers;
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

        // Self-healing nếu Redis bị Cache Miss (chưa Pre-Warm hoặc vừa restart Redis)
        if (!luaResult.Success && (luaResult.ErrorCode == "ERR_AUCTION_CACHE_MISS" || luaResult.ErrorCode == "ERR_PARTICIPANT_CACHE_MISS"))
        {
            var recovered = await TryRecoverAuctionCacheAsync(request.AuctionId, cancellationToken);
            if (!recovered)
            {
                throw new BadRequestException("Phiên đấu giá chưa mở hoặc đã kết thúc.");
            }

            luaResult = await _redisCache.PlaceBidAtomicAsync(
                request.AuctionId,
                participant.Id,
                request.UserId,
                request.BidAmount,
                bidId,
                cancellationToken);
        }

        if (!luaResult.Success && luaResult.ErrorCode != "ERR_REDIS_UNAVAILABLE")
        {
            throw new BadRequestException(luaResult.Message ?? "Lượt đặt giá không hợp lệ.");
        }

        // 2. NẾU REDIS THÀNH CÔNG: ĐỒNG BỘ KÍCH HOẠT (NẾU VỪA JIT), BROADCAST VÀ RETURN NGAY
        if (luaResult.Success)
        {
            if (luaResult.JustActivated)
            {
                await SynchronizeJitActivationAsync(request.AuctionId, luaResult.NewCurrentPrice, cancellationToken);
            }

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

        // 3. FALLBACK CƠ SỞ DỮ LIỆU SQL (KHI REDIS OFFLINE)
        var auction = await _unitOfWork.Auctions.GetByIdAsync(request.AuctionId, cancellationToken);
        var nowUtc = DateTime.UtcNow;
        var jitFromSchedule = auction != null
            && auction.AuctionStatus == AuctionStatus.SCHEDULE
            && nowUtc >= auction.StartAt
            && nowUtc < auction.EndAt
            && auction.Participants.Count > 0;

        if (auction == null || (auction.AuctionStatus != AuctionStatus.ACTIVE && !jitFromSchedule))
            throw new BadRequestException("Phiên đấu giá chưa mở hoặc đã kết thúc.");

        if (nowUtc >= auction.EndAt)
            throw new BadRequestException("Đã hết thời gian đặt giá của phiên.");

        if (jitFromSchedule)
        {
            AuctionActivationHelper.ActivateAndFreezeWallets(auction);
            await _delayQueue.CancelJobAsync(auction.Id, AuctionDelayJobTypes.AUCTION_PRE_WARM, cancellationToken);
            await _delayQueue.CancelJobAsync(auction.Id, AuctionDelayJobTypes.AUCTION_START, cancellationToken);

            var postStartJob = AuctionActivationHelper.BuildPostStartJob(auction);
            if (postStartJob != null)
            {
                await _delayQueue.EnqueueJobAsync(postStartJob, cancellationToken);
            }

            await _realtimeNotifier.NotifyAuctionStatusChangedAsync(
                auction.Id,
                AuctionStatus.ACTIVE.ToString(),
                null,
                auction.CurrentPrice,
                cancellationToken);
        }

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

        // Tối ưu hóa Realtime: Sử dụng LockVersion làm totalBids thay vì thực hiện query SQL Count(*) qua mạng
        var totalBids = (int)auction.LockVersion;

        var maskedNameFallback = participant.UserAccount?.UserName != null && participant.UserAccount.UserName.Length > 3
            ? participant.UserAccount.UserName[..2] + "***" + participant.UserAccount.UserName[^1..]
            : ("***" + request.UserId.ToString()[^4..]);

        var fallbackDto = new BidDto
        {
            Id = fallbackBid.Id,
            AuctionId = fallbackBid.AuctionId,
            ParticipantId = fallbackBid.ParticipantId,
            UserAccountId = participant.UserAccountId,
            UserNameMasked = maskedNameFallback,
            BidAmount = fallbackBid.BidAmount,
            PlacedAt = fallbackBid.PlacedAt,
            Status = fallbackBid.Status
        };

        // Bắn SignalR Broadcast Realtime tức thì tới phòng con của phiên
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

    private async Task<bool> TryRecoverAuctionCacheAsync(Guid auctionId, CancellationToken cancellationToken)
    {
        var auction = await _unitOfWork.Auctions.GetDetailByIdAsync(auctionId, cancellationToken);
        if (auction == null)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        var canParticipateInWindow = auction.AuctionStatus == AuctionStatus.ACTIVE
            || (auction.AuctionStatus == AuctionStatus.SCHEDULE
                && now >= auction.StartAt
                && now < auction.EndAt
                && auction.Participants.Count > 0);

        if (!canParticipateInWindow || now >= auction.EndAt)
        {
            return false;
        }

        await _redisCache.WarmUpAuctionAsync(auction, cancellationToken);
        return true;
    }

    private async Task SynchronizeJitActivationAsync(
        Guid auctionId,
        decimal fallbackPrice,
        CancellationToken cancellationToken)
    {
        var auction = await _unitOfWork.Auctions.GetByIdAsync(auctionId, cancellationToken);
        if (auction == null)
        {
            return;
        }

        var transitionedToActive = false;

        if (auction.AuctionStatus == AuctionStatus.SCHEDULE)
        {
            AuctionActivationHelper.ActivateAndFreezeWallets(auction);
            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                transitionedToActive = true;
            }
            catch
            {
                // Đọc mới hoàn toàn từ DB (tránh entity cũ trong ChangeTracker) nếu PersistenceWorker hoặc Scheduler vừa chạm LockVersion
                var reloaded = await _unitOfWork.Auctions.GetFreshByIdAsync(auctionId, cancellationToken);
                if (reloaded == null)
                {
                    throw;
                }

                if (reloaded.AuctionStatus == AuctionStatus.SCHEDULE)
                {
                    AuctionActivationHelper.ActivateAndFreezeWallets(reloaded);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    auction = reloaded;
                    transitionedToActive = true;
                }
                else if (reloaded.AuctionStatus == AuctionStatus.ACTIVE)
                {
                    auction = reloaded;
                }
                else
                {
                    throw;
                }
            }
        }

        if (!transitionedToActive)
        {
            return;
        }

        await _delayQueue.CancelJobAsync(auctionId, AuctionDelayJobTypes.AUCTION_PRE_WARM, cancellationToken);
        await _delayQueue.CancelJobAsync(auctionId, AuctionDelayJobTypes.AUCTION_START, cancellationToken);

        var postStartJob = AuctionActivationHelper.BuildPostStartJob(auction);
        if (postStartJob != null)
        {
            await _delayQueue.EnqueueJobAsync(postStartJob, cancellationToken);
        }

        await _realtimeNotifier.NotifyAuctionStatusChangedAsync(
            auctionId,
            AuctionStatus.ACTIVE.ToString(),
            null,
            auction.CurrentPrice > 0 ? auction.CurrentPrice : fallbackPrice,
            cancellationToken);
    }
}
