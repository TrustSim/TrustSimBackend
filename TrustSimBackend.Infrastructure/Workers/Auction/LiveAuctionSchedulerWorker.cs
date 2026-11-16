using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Features.Auction.Services;

namespace TrustSimBackend.Infrastructure.Workers.Auction;

/// <summary>
/// Worker ưu tiên cao điều phối vòng đời phiên đấu giá trực tiếp (AUCTION_PRE_WARM, AUCTION_START, DUTCH_PRICE_DROP, AUCTION_END).
/// Tách biệt hoàn toàn khỏi DB Sync và các job hậu đấu giá để mở/đóng phiên đạt độ trễ tối thiểu.
/// </summary>
public sealed class LiveAuctionSchedulerWorker : DelayQueueWorkerBase
{
    public LiveAuctionSchedulerWorker(
        IServiceScopeFactory scopeFactory,
        IAuctionDelayQueueService delayQueue,
        ILogger<LiveAuctionSchedulerWorker> logger)
        : base(scopeFactory, delayQueue, logger)
    {
    }

    protected override AuctionDelayQueueLane Lane => AuctionDelayQueueLane.Live;
    protected override string WorkerName => "LiveAuctionScheduler";
    protected override TimeSpan IdleWaitCap => TimeSpan.FromSeconds(15);
    protected override int MaxConcurrency => 8;
}
