using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TrustSimBackend.Application.Features.Auction.Services;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Cài đặt Dịch vụ Hàng đợi Trễ phân tán tách 2 lane (Live Auction ưu tiên cao &amp; Post-Auction Deadlines)
/// trên Redis ZSET (Sorted Set) kết hợp In-Memory Fallback và tín hiệu đánh thức tức thì.
/// </summary>
public class RedisDelayQueueService : IAuctionDelayQueueService
{
    private const string LiveQueueKey = "auction:delay_queue:live";
    private const string PostAuctionQueueKey = "auction:delay_queue:post";

    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<RedisDelayQueueService> _logger;
    private readonly object _fallbackLock = new();

    private readonly SortedSet<(long Score, string Payload)> _inMemoryLiveQueue = CreateFallbackQueue();
    private readonly SortedSet<(long Score, string Payload)> _inMemoryPostQueue = CreateFallbackQueue();

    private readonly SemaphoreSlim _liveWakeSignal = new(0, 1);
    private readonly SemaphoreSlim _postWakeSignal = new(0, 1);

    public RedisDelayQueueService(
        IConfiguration configuration,
        ILogger<RedisDelayQueueService> logger,
        IConnectionMultiplexer? redis = null)
    {
        _logger = logger;
        _redis = redis;

        if (_redis == null)
        {
            var redisConnection = configuration.GetConnectionString("Redis") ?? configuration["Redis:ConnectionString"];
            if (!string.IsNullOrWhiteSpace(redisConnection))
            {
                try
                {
                    var configOptions = ConfigurationOptions.Parse(redisConnection);
                    configOptions.AbortOnConnectFail = false;
                    configOptions.ConnectTimeout = 10000;
                    configOptions.SyncTimeout = 10000;
                    configOptions.KeepAlive = 60;

                    _redis = ConnectionMultiplexer.Connect(configOptions);
                    _logger.LogInformation("[RedisDelayQueue] Đã kết nối thành công tới Redis: {Endpoints}", string.Join(", ", configOptions.EndPoints));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[RedisDelayQueue] Không thể kết nối Redis. Tự động chuyển sang chế độ In-Memory Delay Queue.");
                }
            }
            else
            {
                _logger.LogInformation("[RedisDelayQueue] Không tìm thấy cấu hình Redis ConnectionString. Sử dụng In-Memory Delay Queue.");
            }
        }
    }

    public Task EnqueueJobAsync(AuctionDelayJob job, CancellationToken cancellationToken = default)
        => EnqueueJobInternalAsync(job, preserveLaterExisting: false, cancellationToken);

    public Task EnqueueJobPreservingLaterAsync(AuctionDelayJob job, CancellationToken cancellationToken = default)
        => EnqueueJobInternalAsync(job, preserveLaterExisting: true, cancellationToken);

    private async Task EnqueueJobInternalAsync(
        AuctionDelayJob job,
        bool preserveLaterExisting,
        CancellationToken cancellationToken)
    {
        var lane = AuctionDelayJobTypes.GetLane(job.JobType);
        var queueKey = GetQueueKey(lane);
        var payload = JsonSerializer.Serialize(job);
        var score = new DateTimeOffset(job.ExecuteAt.ToUniversalTime()).ToUnixTimeMilliseconds();

        if (_redis != null && _redis.IsConnected)
        {
            try
            {
                var db = _redis.GetDatabase();
                var entries = await db.SortedSetRangeByRankWithScoresAsync(queueKey);
                foreach (var entry in entries)
                {
                    var json = (string?)entry.Element;
                    if (string.IsNullOrWhiteSpace(json)) continue;

                    try
                    {
                        var existingJob = JsonSerializer.Deserialize<AuctionDelayJob>(json);
                        if (existingJob != null && existingJob.AuctionId == job.AuctionId && existingJob.JobType == job.JobType)
                        {
                            var existingScore = (long)entry.Score;
                            if (existingScore == score || (preserveLaterExisting && existingScore > score))
                            {
                                return;
                            }

                            await db.SortedSetRemoveAsync(queueKey, entry.Element);
                        }
                    }
                    catch { }
                }

                await db.SortedSetAddAsync(queueKey, payload, score);
                PulseWakeUp(lane);
                _logger.LogInformation("[RedisDelayQueue:{Lane}] ✅ Lập lịch {JobType} cho phiên {AuctionId} lúc {ExecuteAt:HH:mm:ss.fff dd/MM/yyyy}",
                    lane, job.JobType, job.AuctionId, job.ExecuteAt);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[RedisDelayQueue:{Lane}] Lỗi ghi Redis ZSET. Fallback sang In-Memory Queue.", lane);
            }
        }

        // In-Memory Fallback
        var inserted = false;
        lock (_fallbackLock)
        {
            var queue = GetFallbackQueue(lane);
            var toRemove = new List<(long Score, string Payload)>();
            foreach (var item in queue)
            {
                try
                {
                    var existingJob = JsonSerializer.Deserialize<AuctionDelayJob>(item.Payload);
                    if (existingJob != null && existingJob.AuctionId == job.AuctionId && existingJob.JobType == job.JobType)
                    {
                        if (item.Score == score || (preserveLaterExisting && item.Score > score))
                        {
                            return;
                        }

                        toRemove.Add(item);
                    }
                }
                catch { }
            }

            foreach (var item in toRemove)
            {
                queue.Remove(item);
            }

            queue.Add((score, payload));
            inserted = true;
        }

        if (inserted)
        {
            PulseWakeUp(lane);
            _logger.LogInformation("[RedisDelayQueue (In-Memory:{Lane})] ✅ Lập lịch {JobType} cho phiên {AuctionId} lúc {ExecuteAt:HH:mm:ss.fff dd/MM/yyyy}",
                lane, job.JobType, job.AuctionId, job.ExecuteAt);
        }
    }

    public async Task CancelJobAsync(Guid auctionId, string? jobType = null, CancellationToken cancellationToken = default)
    {
        var lanes = string.IsNullOrWhiteSpace(jobType)
            ? new[] { AuctionDelayQueueLane.Live, AuctionDelayQueueLane.PostAuction }
            : new[] { AuctionDelayJobTypes.GetLane(jobType) };

        if (_redis != null && _redis.IsConnected)
        {
            try
            {
                var db = _redis.GetDatabase();
                foreach (var lane in lanes)
                {
                    var queueKey = GetQueueKey(lane);
                    var entries = await db.SortedSetRangeByRankAsync(queueKey);
                    foreach (var entry in entries)
                    {
                        var json = (string?)entry;
                        if (string.IsNullOrWhiteSpace(json)) continue;

                        try
                        {
                            var job = JsonSerializer.Deserialize<AuctionDelayJob>(json);
                            if (job != null && job.AuctionId == auctionId && (jobType == null || job.JobType == jobType))
                            {
                                await db.SortedSetRemoveAsync(queueKey, entry);
                                _logger.LogInformation("[RedisDelayQueue:{Lane}] 🗑️ Đã hủy Job {JobType} của phiên {AuctionId}", lane, job.JobType, auctionId);
                            }
                        }
                        catch { }
                    }
                }
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[RedisDelayQueue] Lỗi khi hủy job từ Redis.");
            }
        }

        // In-Memory Fallback
        lock (_fallbackLock)
        {
            foreach (var lane in lanes)
            {
                var queue = GetFallbackQueue(lane);
                var toRemove = new List<(long Score, string Payload)>();
                foreach (var item in queue)
                {
                    try
                    {
                        var job = JsonSerializer.Deserialize<AuctionDelayJob>(item.Payload);
                        if (job != null && job.AuctionId == auctionId && (jobType == null || job.JobType == jobType))
                        {
                            toRemove.Add(item);
                        }
                    }
                    catch { }
                }

                foreach (var item in toRemove)
                {
                    queue.Remove(item);
                }
            }
        }
    }

    public async Task<AuctionDelayJob?> PopNextDueJobAsync(
        DateTime now,
        AuctionDelayQueueLane lane = AuctionDelayQueueLane.Live,
        CancellationToken cancellationToken = default)
    {
        var queueKey = GetQueueKey(lane);
        var nowScore = new DateTimeOffset(now.ToUniversalTime()).ToUnixTimeMilliseconds();

        if (_redis != null && _redis.IsConnected)
        {
            try
            {
                var db = _redis.GetDatabase();
                // Lua script nguyên tử: Lấy job có score <= nowScore và xóa ngay lập tức
                const string script = @"
                    local jobs = redis.call('ZRANGEBYSCORE', KEYS[1], 0, ARGV[1], 'LIMIT', 0, 1)
                    if #jobs > 0 then
                        redis.call('ZREM', KEYS[1], jobs[1])
                        return jobs[1]
                    end
                    return nil";

                var result = await db.ScriptEvaluateAsync(script, new RedisKey[] { queueKey }, new RedisValue[] { nowScore });
                if (!result.IsNull)
                {
                    var json = (string?)result;
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        return JsonSerializer.Deserialize<AuctionDelayJob>(json);
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[RedisDelayQueue:{Lane}] Lỗi pop job từ Redis. Fallback sang In-Memory.", lane);
            }
        }

        // In-Memory Fallback
        lock (_fallbackLock)
        {
            var queue = GetFallbackQueue(lane);
            if (queue.Count == 0) return null;

            var first = queue.Min;
            if (first.Score <= nowScore)
            {
                queue.Remove(first);
                return JsonSerializer.Deserialize<AuctionDelayJob>(first.Payload);
            }
        }

        return null;
    }

    public async Task<DateTime?> GetNextJobTimeAsync(
        AuctionDelayQueueLane lane = AuctionDelayQueueLane.Live,
        CancellationToken cancellationToken = default)
    {
        var queueKey = GetQueueKey(lane);

        if (_redis != null && _redis.IsConnected)
        {
            try
            {
                var db = _redis.GetDatabase();
                var entries = await db.SortedSetRangeByRankWithScoresAsync(queueKey, 0, 0);
                if (entries.Length > 0)
                {
                    var score = (long)entries[0].Score;
                    return DateTimeOffset.FromUnixTimeMilliseconds(score).UtcDateTime;
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[RedisDelayQueue:{Lane}] Lỗi lấy score kế tiếp từ Redis.", lane);
            }
        }

        // In-Memory Fallback
        lock (_fallbackLock)
        {
            var queue = GetFallbackQueue(lane);
            if (queue.Count > 0)
            {
                var first = queue.Min;
                return DateTimeOffset.FromUnixTimeMilliseconds(first.Score).UtcDateTime;
            }
        }

        return null;
    }

    public async Task WaitForSignalOrDelayAsync(
        AuctionDelayQueueLane lane,
        TimeSpan delay,
        CancellationToken cancellationToken = default)
    {
        if (delay <= TimeSpan.Zero)
        {
            return;
        }

        var signal = GetWakeSignal(lane);
        await signal.WaitAsync(delay, cancellationToken);
    }

    private void PulseWakeUp(AuctionDelayQueueLane lane)
    {
        var signal = GetWakeSignal(lane);
        if (signal.CurrentCount == 0)
        {
            try
            {
                signal.Release();
            }
            catch (SemaphoreFullException)
            {
                // Đã có tín hiệu chờ sẵn
            }
        }
    }

    private SemaphoreSlim GetWakeSignal(AuctionDelayQueueLane lane)
        => lane == AuctionDelayQueueLane.Live ? _liveWakeSignal : _postWakeSignal;

    private static string GetQueueKey(AuctionDelayQueueLane lane)
        => lane == AuctionDelayQueueLane.Live ? LiveQueueKey : PostAuctionQueueKey;

    private SortedSet<(long Score, string Payload)> GetFallbackQueue(AuctionDelayQueueLane lane)
        => lane == AuctionDelayQueueLane.Live ? _inMemoryLiveQueue : _inMemoryPostQueue;

    private static SortedSet<(long Score, string Payload)> CreateFallbackQueue()
        => new(Comparer<(long Score, string Payload)>.Create((a, b) =>
        {
            var cmp = a.Score.CompareTo(b.Score);
            return cmp != 0 ? cmp : string.Compare(a.Payload, b.Payload, StringComparison.Ordinal);
        }));
}
