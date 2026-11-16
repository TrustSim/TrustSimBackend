using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;
using TrustSimBackend.Application.Features.Auction.Helpers;
using TrustSimBackend.Application.Features.Auction.Hubs;
using TrustSimBackend.Application.Features.Auction.Services;
using TrustSimBackend.Application.Features.Auction.States;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Commands;

public class ActivateAuctionCommandHandler : IRequestHandler<ActivateAuctionCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuctionRealtimeNotifier _realtimeNotifier;
    private readonly IRedisAuctionCacheService? _redisCache;
    private readonly IAuctionDelayQueueService? _delayQueue;

    public ActivateAuctionCommandHandler(
        IUnitOfWork unitOfWork,
        IAuctionRealtimeNotifier realtimeNotifier,
        IRedisAuctionCacheService? redisCache = null,
        IAuctionDelayQueueService? delayQueue = null)
    {
        _unitOfWork = unitOfWork;
        _realtimeNotifier = realtimeNotifier;
        _redisCache = redisCache;
        _delayQueue = delayQueue;
    }

    public async Task<bool> Handle(ActivateAuctionCommand request, CancellationToken cancellationToken)
    {
        var auction = await _unitOfWork.Auctions.GetByIdAsync(request.AuctionId, cancellationToken);
        if (auction == null)
        {
            throw new NotFoundException($"Không tìm thấy phiên đấu giá với ID '{request.AuctionId}'.");
        }

        if (auction.Participants.Count == 0)
        {
            throw new BadRequestException("Không thể kích hoạt phiên đấu giá khi chưa có người tham gia đặt cọc.");
        }

        var state = AuctionStateContext.GetState(auction.AuctionStatus);
        state.Activate(auction);
        AuctionActivationHelper.ActivateAndFreezeWallets(auction);

        _unitOfWork.Auctions.Update(auction);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (_redisCache != null)
        {
            await _redisCache.WarmUpAuctionAsync(auction, cancellationToken);
        }

        if (_delayQueue != null)
        {
            await _delayQueue.CancelJobAsync(auction.Id, AuctionDelayJobTypes.AUCTION_PRE_WARM, cancellationToken);
            await _delayQueue.CancelJobAsync(auction.Id, AuctionDelayJobTypes.AUCTION_START, cancellationToken);

            var nextJob = AuctionActivationHelper.BuildPostStartJob(auction);
            if (nextJob != null)
            {
                await _delayQueue.EnqueueJobAsync(nextJob, cancellationToken);
            }
        }

        await _realtimeNotifier.NotifyAuctionStatusChangedAsync(
            auction.Id,
            auction.AuctionStatus.ToString(),
            null,
            auction.CurrentPrice,
            cancellationToken);

        return true;
    }
}
