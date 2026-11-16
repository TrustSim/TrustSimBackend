using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Features.Auction.Services;
using TrustSimBackend.Infrastructure.Workers.Auction.DelayJobs;

namespace TrustSimBackend.Infrastructure.Workers.Auction;

/// <summary>
/// Lớp nền chung cho các Worker điều phối hàng đợi trễ theo từng lane (Live vs PostAuction).
/// Hỗ trợ đánh thức tức thì khi có job mới và chạy song song giữa các phiên khác nhau nhưng tuần tự trong cùng một phiên.
/// </summary>
public abstract class DelayQueueWorkerBase : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAuctionDelayQueueService _delayQueue;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _auctionLocks = new();
    private readonly ConcurrentDictionary<Task, byte> _inFlightTasks = new();

    protected DelayQueueWorkerBase(
        IServiceScopeFactory scopeFactory,
        IAuctionDelayQueueService delayQueue,
        ILogger logger)
    {
        _scopeFactory = scopeFactory;
        _delayQueue = delayQueue;
        _logger = logger;
    }

    protected abstract AuctionDelayQueueLane Lane { get; }
    protected abstract string WorkerName { get; }
    protected virtual TimeSpan IdleWaitCap => TimeSpan.FromSeconds(15);
    protected virtual int MaxConcurrency => 8;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[{Worker}] Khởi động bộ điều phối hàng đợi lane '{Lane}'...", WorkerName, Lane);
        using var concurrencyGate = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                var dispatchedAny = false;

                while (!stoppingToken.IsCancellationRequested)
                {
                    var job = await _delayQueue.PopNextDueJobAsync(now, Lane, stoppingToken);
                    if (job == null)
                    {
                        break;
                    }

                    dispatchedAny = true;
                    _logger.LogInformation("[{Worker}] ⚡ Thực thi Job {JobType} cho phiên {AuctionId} lúc {Time:HH:mm:ss.fff}",
                        WorkerName, job.JobType, job.AuctionId, now);

                    await concurrencyGate.WaitAsync(stoppingToken);
                    var task = ExecuteJobIsolatedAsync(job, concurrencyGate, stoppingToken);
                    _inFlightTasks.TryAdd(task, 0);
                    _ = task.ContinueWith(
                        static (t, state) => ((ConcurrentDictionary<Task, byte>)state!).TryRemove(t, out _),
                        _inFlightTasks,
                        TaskScheduler.Default);
                }

                if (dispatchedAny)
                {
                    continue;
                }

                var nextJobTime = await _delayQueue.GetNextJobTimeAsync(Lane, stoppingToken);
                var waitDuration = IdleWaitCap;

                if (nextJobTime.HasValue)
                {
                    var remaining = nextJobTime.Value - DateTime.UtcNow;
                    if (remaining <= TimeSpan.Zero)
                    {
                        continue;
                    }

                    if (remaining < waitDuration)
                    {
                        waitDuration = remaining;
                    }
                }

                await _delayQueue.WaitForSignalOrDelayAsync(Lane, waitDuration, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Worker}] Lỗi trong vòng lặp điều phối lane '{Lane}'.", WorkerName, Lane);
                try
                {
                    await Task.Delay(500, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        var remainingTasks = _inFlightTasks.Keys.ToArray();
        if (remainingTasks.Length > 0)
        {
            try
            {
                await Task.WhenAll(remainingTasks);
            }
            catch
            {
                // Đã log lỗi ở từng task
            }
        }

        _logger.LogInformation("[{Worker}] Dừng bộ điều phối lane '{Lane}'.", WorkerName, Lane);
    }

    private async Task ExecuteJobIsolatedAsync(
        AuctionDelayJob job,
        SemaphoreSlim concurrencyGate,
        CancellationToken stoppingToken)
    {
        var auctionLock = _auctionLocks.GetOrAdd(job.AuctionId, static _ => new SemaphoreSlim(1, 1));
        var lockAcquired = false;

        try
        {
            await auctionLock.WaitAsync(stoppingToken);
            lockAcquired = true;

            using var scope = _scopeFactory.CreateScope();
            var handlers = scope.ServiceProvider.GetServices<IAuctionDelayJobHandler>();
            var handler = handlers.FirstOrDefault(h => h.CanHandle(job.JobType));

            if (handler == null)
            {
                _logger.LogWarning("[{Worker}] Không tìm thấy handler cho JobType '{JobType}'", WorkerName, job.JobType);
                return;
            }

            await handler.HandleAsync(job, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // App đang shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Worker}] Lỗi khi xử lý Job {JobType} cho phiên {AuctionId}",
                WorkerName, job.JobType, job.AuctionId);
        }
        finally
        {
            if (lockAcquired)
            {
                auctionLock.Release();
            }

            concurrencyGate.Release();
        }
    }
}
