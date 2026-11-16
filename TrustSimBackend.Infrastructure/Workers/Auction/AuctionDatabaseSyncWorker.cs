using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Auction.Services;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Infrastructure.Persistence;

namespace TrustSimBackend.Infrastructure.Workers.Auction;

/// <summary>
/// Worker nền chạy độc lập mỗi 15 giây để đồng bộ phiên &amp; deadline từ PostgreSQL sang Redis Delay Queue
/// và tự động khôi phục Redis Hot Cache cho các phiên ACTIVE bị thiếu cache (sau khi restart server/Redis).
/// </summary>
public sealed class AuctionDatabaseSyncWorker : BackgroundService
{
    private static readonly TimeSpan SyncInterval = TimeSpan.FromSeconds(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAuctionDelayQueueService _delayQueue;
    private readonly ILogger<AuctionDatabaseSyncWorker> _logger;
    private readonly IRedisAuctionCacheService? _redisCache;

    public AuctionDatabaseSyncWorker(
        IServiceScopeFactory scopeFactory,
        IAuctionDelayQueueService delayQueue,
        ILogger<AuctionDatabaseSyncWorker> logger,
        IRedisAuctionCacheService? redisCache = null)
    {
        _scopeFactory = scopeFactory;
        _delayQueue = delayQueue;
        _logger = logger;
        _redisCache = redisCache;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[AuctionDbSyncWorker] Khởi động bộ đồng bộ DB → Delay Queue & Redis Cache Recovery...");

        await SafeSyncAsync(isStartup: true, stoppingToken);

        using var timer = new PeriodicTimer(SyncInterval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await SafeSyncAsync(isStartup: false, stoppingToken);
        }

        _logger.LogInformation("[AuctionDbSyncWorker] Dừng bộ đồng bộ DB.");
    }

    private async Task SafeSyncAsync(bool isStartup, CancellationToken cancellationToken)
    {
        try
        {
            await SyncDatabaseAuctionsToDelayQueueAsync(isStartup, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Bỏ qua khi app shutdown
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AuctionDbSyncWorker] Lỗi khi đồng bộ DB sang Delay Queue (Startup={IsStartup}).", isStartup);
        }
    }

    public async Task SyncDatabaseAuctionsToDelayQueueAsync(bool isStartup, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrustSimDbContext>();
        var now = DateTime.UtcNow;

        // 1. Phiên SCHEDULE -> Nạp AUCTION_PRE_WARM (T - 5s) và AUCTION_START
        var scheduledAuctions = await context.Auctions
            .AsNoTracking()
            .Where(a => a.AuctionStatus == AuctionStatus.SCHEDULE && a.IsActive)
            .Select(a => new { a.Id, a.StartAt })
            .ToListAsync(cancellationToken);

        foreach (var auc in scheduledAuctions)
        {
            if (auc.StartAt > now.AddSeconds(1))
            {
                var preWarmAt = auc.StartAt.AddSeconds(-5);
                if (preWarmAt > now)
                {
                    await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
                    {
                        AuctionId = auc.Id,
                        JobType = AuctionDelayJobTypes.AUCTION_PRE_WARM,
                        ExecuteAt = preWarmAt
                    }, cancellationToken);
                }
                else if (_redisCache == null || !await _redisCache.IsAuctionWarmedUpAsync(auc.Id, requireActiveStatus: false, cancellationToken))
                {
                    await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
                    {
                        AuctionId = auc.Id,
                        JobType = AuctionDelayJobTypes.AUCTION_PRE_WARM,
                        ExecuteAt = now
                    }, cancellationToken);
                }
            }

            await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
            {
                AuctionId = auc.Id,
                JobType = AuctionDelayJobTypes.AUCTION_START,
                ExecuteAt = auc.StartAt
            }, cancellationToken);
        }

        // 2. Phiên ACTIVE -> Nạp AUCTION_END / DUTCH_PRICE_DROP & tự phục hồi Redis cache nếu thiếu
        var activeAuctions = await context.Auctions
            .Include(a => a.AuctionRule)
            .Include(a => a.Participants)
                .ThenInclude(p => p.Deposits)
            .Include(a => a.Participants)
                .ThenInclude(p => p.UserAccount)
                    .ThenInclude(u => u!.Wallet)
            .Include(a => a.Bids)
            .AsNoTracking()
            .Where(a => a.AuctionStatus == AuctionStatus.ACTIVE && a.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var auc in activeAuctions)
        {
            if (_redisCache != null && !await _redisCache.IsAuctionWarmedUpAsync(auc.Id, requireActiveStatus: true, cancellationToken))
            {
                await _redisCache.WarmUpAuctionAsync(auc, cancellationToken);
            }

            if (auc.AuctionType == AuctionType.ENGLISH)
            {
                var cachedEndAt = _redisCache != null
                    ? await _redisCache.GetCachedEndAtUtcAsync(auc.Id, cancellationToken)
                    : null;
                var effectiveEndAt = cachedEndAt.HasValue && cachedEndAt.Value > auc.EndAt
                    ? cachedEndAt.Value
                    : auc.EndAt;

                await _delayQueue.EnqueueJobPreservingLaterAsync(new AuctionDelayJob
                {
                    AuctionId = auc.Id,
                    JobType = AuctionDelayJobTypes.AUCTION_END,
                    ExecuteAt = effectiveEndAt
                }, cancellationToken);
            }
            else if (auc.AuctionType == AuctionType.DUTCH && auc.AuctionRule != null)
            {
                var interval = auc.AuctionRule.DutchDecreaseIntervalSeconds > 0 ? auc.AuctionRule.DutchDecreaseIntervalSeconds : 60;
                var elapsed = (now - auc.StartAt).TotalSeconds;
                var cycles = Math.Max(0, (int)(elapsed / interval));
                var nextDropAt = auc.StartAt.AddSeconds((cycles + 1) * interval);

                await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
                {
                    AuctionId = auc.Id,
                    JobType = AuctionDelayJobTypes.DUTCH_PRICE_DROP,
                    ExecuteAt = nextDropAt
                }, cancellationToken);
            }
        }

        // 3. Kết quả hậu đấu giá đang chờ xử lý
        var pendingPayments = await context.AuctionResults
            .AsNoTracking()
            .Where(r => r.AuctionResultStatus == AuctionResultStatus.PENDING_PAYMENT && r.PaymentDeadline.HasValue)
            .Select(r => new { r.AuctionId, Deadline = r.PaymentDeadline!.Value })
            .ToListAsync(cancellationToken);

        foreach (var res in pendingPayments)
        {
            await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
            {
                AuctionId = res.AuctionId,
                JobType = AuctionDelayJobTypes.PAYMENT_DEADLINE,
                ExecuteAt = res.Deadline
            }, cancellationToken);
        }

        var pendingRunnerUps = await context.AuctionResults
            .AsNoTracking()
            .Where(r => r.AuctionResultStatus == AuctionResultStatus.RUNNER_UP_PENDING && r.RunnerUpPaymentDeadline.HasValue)
            .Select(r => new { r.AuctionId, Deadline = r.RunnerUpPaymentDeadline!.Value })
            .ToListAsync(cancellationToken);

        foreach (var res in pendingRunnerUps)
        {
            await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
            {
                AuctionId = res.AuctionId,
                JobType = AuctionDelayJobTypes.RUNNER_UP_DEADLINE,
                ExecuteAt = res.Deadline
            }, cancellationToken);
        }

        var pendingSellerDecisions = await context.AuctionResults
            .AsNoTracking()
            .Where(r => r.AuctionResultStatus == AuctionResultStatus.PENDING_SELLER_CONFIRMATION && r.SellerDecisionDeadline.HasValue)
            .Select(r => new { r.AuctionId, Deadline = r.SellerDecisionDeadline!.Value })
            .ToListAsync(cancellationToken);

        foreach (var res in pendingSellerDecisions)
        {
            await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
            {
                AuctionId = res.AuctionId,
                JobType = AuctionDelayJobTypes.SELLER_DECISION_DEADLINE,
                ExecuteAt = res.Deadline
            }, cancellationToken);
        }

        if (isStartup)
        {
            _logger.LogInformation(
                "[AuctionDbSyncWorker] Đồng bộ Startup hoàn tất ({SchedCount} scheduled, {ActCount} active, {PayCount} pending payments, {SellerCount} pending seller).",
                scheduledAuctions.Count, activeAuctions.Count, pendingPayments.Count, pendingSellerDecisions.Count);
        }
    }
}
