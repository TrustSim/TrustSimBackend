using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TrustSimBackend.Application.Features.Auction.Services;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Cài đặt Dịch vụ Hàng đợi Trễ phân tán sử dụng Redis ZSET (Sorted Set) kết hợp In-Memory Fallback.
/// </summary>
public class RedisDelayQueueService : IAuctionDelayQueueService
{
    private const string QueueKey = "auction:delay_queue";
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<RedisDelayQueueService> _logger;
    private readonly object _fallbackLock = new();
    private readonly SortedSet<(long Score, string Payload)> _inMemoryQueue = new(Comparer<(long Score, string Payload)>.Create((a, b) =>
    {
        var cmp = a.Score.CompareTo(b.Score);
        return cmp != 0 ? cmp : string.Compare(a.Payload, b.Payload, StringComparison.Ordinal);
    }));

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

    public async Task EnqueueJobAsync(AuctionDelayJob job, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(job);
        var score = new DateTimeOffset(job.ExecuteAt.ToUniversalTime()).ToUnixTimeMilliseconds();

        if (_redis != null && _redis.IsConnected)
        {
            try
            {
                var db = _redis.GetDatabase();
                await db.SortedSetAddAsync(QueueKey, payload, score);
                _logger.LogInformation("[RedisDelayQueue] ✅ Đã lập lịch Job {JobType} cho phiên {AuctionId} lúc {ExecuteAt:HH:mm:ss dd/MM/yyyy}",
                    job.JobType, job.AuctionId, job.ExecuteAt);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[RedisDelayQueue] Lỗi khi ghi vào Redis ZSET. Fallback sang In-Memory Queue.");
            }
        }

        // In-Memory Fallback
        lock (_fallbackLock)
        {
            _inMemoryQueue.Add((score, payload));
            _logger.LogInformation("[RedisDelayQueue (In-Memory)] ✅ Đã lập lịch Job {JobType} cho phiên {AuctionId} lúc {ExecuteAt:HH:mm:ss dd/MM/yyyy}",
                job.JobType, job.AuctionId, job.ExecuteAt);
        }
    }

    public async Task CancelJobAsync(Guid auctionId, string? jobType = null, CancellationToken cancellationToken = default)
    {
        if (_redis != null && _redis.IsConnected)
        {
            try
            {
                var db = _redis.GetDatabase();
                var entries = await db.SortedSetRangeByRankAsync(QueueKey);
                foreach (var entry in entries)
                {
                    var json = (string?)entry;
                    if (string.IsNullOrWhiteSpace(json)) continue;

                    try
                    {
                        var job = JsonSerializer.Deserialize<AuctionDelayJob>(json);
                        if (job != null && job.AuctionId == auctionId && (jobType == null || job.JobType == jobType))
                        {
                            await db.SortedSetRemoveAsync(QueueKey, entry);
                            _logger.LogInformation("[RedisDelayQueue] 🗑️ Đã hủy Job {JobType} của phiên {AuctionId}", job.JobType, auctionId);
                        }
                    }
                    catch { }
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
            var toRemove = new List<(long Score, string Payload)>();
            foreach (var item in _inMemoryQueue)
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
                _inMemoryQueue.Remove(item);
            }
        }
    }

    public async Task<AuctionDelayJob?> PopNextDueJobAsync(DateTime now, CancellationToken cancellationToken = default)
    {
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

                var result = await db.ScriptEvaluateAsync(script, new RedisKey[] { QueueKey }, new RedisValue[] { nowScore });
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
                _logger.LogWarning(ex, "[RedisDelayQueue] Lỗi khi pop job từ Redis. Fallback sang In-Memory.");
            }
        }

        // In-Memory Fallback
        lock (_fallbackLock)
        {
            if (_inMemoryQueue.Count == 0) return null;

            var first = _inMemoryQueue.Min;
            if (first.Score <= nowScore)
            {
                _inMemoryQueue.Remove(first);
                return JsonSerializer.Deserialize<AuctionDelayJob>(first.Payload);
            }
        }

        return null;
    }

    public async Task<DateTime?> GetNextJobTimeAsync(CancellationToken cancellationToken = default)
    {
        if (_redis != null && _redis.IsConnected)
        {
            try
            {
                var db = _redis.GetDatabase();
                var entries = await db.SortedSetRangeByRankWithScoresAsync(QueueKey, 0, 0);
                if (entries.Length > 0)
                {
                    var score = (long)entries[0].Score;
                    return DateTimeOffset.FromUnixTimeMilliseconds(score).UtcDateTime;
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[RedisDelayQueue] Lỗi khi lấy score kế tiếp từ Redis.");
            }
        }

        // In-Memory Fallback
        lock (_fallbackLock)
        {
            if (_inMemoryQueue.Count > 0)
            {
                var first = _inMemoryQueue.Min;
                return DateTimeOffset.FromUnixTimeMilliseconds(first.Score).UtcDateTime;
            }
        }

        return null;
    }
}
