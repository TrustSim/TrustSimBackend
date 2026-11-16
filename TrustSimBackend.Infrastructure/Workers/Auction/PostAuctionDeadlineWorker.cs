using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Features.Auction.Services;

namespace TrustSimBackend.Infrastructure.Workers.Auction;

/// <summary>
/// Worker điều phối các mốc quá hạn hậu đấu giá (Thanh toán, Á quân, Duyệt bán, Ký hợp đồng, Chuyển nhượng, Thanh lý).
/// </summary>
public sealed class PostAuctionDeadlineWorker : DelayQueueWorkerBase
{
    public PostAuctionDeadlineWorker(
        IServiceScopeFactory scopeFactory,
        IAuctionDelayQueueService delayQueue,
        ILogger<PostAuctionDeadlineWorker> logger)
        : base(scopeFactory, delayQueue, logger)
    {
    }

    protected override AuctionDelayQueueLane Lane => AuctionDelayQueueLane.PostAuction;
    protected override string WorkerName => "PostAuctionDeadlineWorker";
    protected override TimeSpan IdleWaitCap => TimeSpan.FromSeconds(30);
    protected override int MaxConcurrency => 4;
}
