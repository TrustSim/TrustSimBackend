using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Dịch vụ Hot-Cache In-Memory Redis quản lý trạng thái phiên, ZSET Leaderboard và thực thi Lua Script nguyên tử.
/// </summary>
public class RedisAuctionCacheService : IRedisAuctionCacheService
{
    private const string EmbeddedLuaResourceName = "TrustSimBackend.Infrastructure.Scripts.place_bid.lua";
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<RedisAuctionCacheService> _logger;
    private static string? _cachedLuaScript;
    private static readonly object _scriptLock = new();

    public RedisAuctionCacheService(
        IConfiguration configuration,
        ILogger<RedisAuctionCacheService> logger,
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
                    _logger.LogInformation("[RedisAuctionCache] Kết nối Redis thành công.");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[RedisAuctionCache] Không thể kết nối Redis. Tự động fallback.");
                }
            }
        }
    }

    /// <summary>
    /// Nạp duy nhất từ tệp Scripts/place_bid.lua (hoặc EmbeddedResource đi kèm assembly) để tránh trùng lặp script.
    /// </summary>
    public static string LoadLuaScript()
    {
        if (_cachedLuaScript != null) return _cachedLuaScript;
        lock (_scriptLock)
        {
            if (_cachedLuaScript != null) return _cachedLuaScript;

            var scriptPath = Path.Combine(AppContext.BaseDirectory, "Scripts", "place_bid.lua");
            if (File.Exists(scriptPath))
            {
                _cachedLuaScript = File.ReadAllText(scriptPath);
                return _cachedLuaScript;
            }

            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(EmbeddedLuaResourceName)
                ?? throw new InvalidOperationException($"Không tìm thấy Lua script '{EmbeddedLuaResourceName}'.");
            using var reader = new StreamReader(stream);
            _cachedLuaScript = reader.ReadToEnd();
            return _cachedLuaScript;
        }
    }

    public async Task WarmUpAuctionAsync(Auction auction, CancellationToken cancellationToken = default)
    {
        if (_redis == null || !_redis.IsConnected) return;

        try
        {
            var db = _redis.GetDatabase();
            var stateKey = $"auction:{auction.Id}:state";
            var rule = auction.AuctionRule;

            var startAtMs = new DateTimeOffset(auction.StartAt.ToUniversalTime()).ToUnixTimeMilliseconds();
            var endAtMs = new DateTimeOffset(auction.EndAt.ToUniversalTime()).ToUnixTimeMilliseconds();
            var participantCount = auction.Participants?.Count ?? 0;

            var hashEntries = new List<HashEntry>
            {
                new("auctionId", auction.Id.ToString()),
                new("status", auction.AuctionStatus.ToString()),
                new("currentPrice", (long)auction.CurrentPrice),
                new("startingPrice", (long)auction.StartingPrice),
                new("minIncrement", (long)(rule?.MinimumBidIncrement ?? 100000)),
                new("maxMultiplier", rule?.MaxStepMultiplier ?? 10),
                new("marginRate", (double)(rule?.MarginPercentage ?? 0.10m)),
                new("startAtMs", startAtMs),
                new("endAtMs", endAtMs),
                new("participantCount", participantCount),
                new("lockVersion", (long)auction.LockVersion),
                new("extensionThresholdMs", (long)((rule?.ExtensionThresholdSeconds ?? 30) * 1000)),
                new("extensionDurationMs", (long)((rule?.ExtensionDurationSeconds ?? 30) * 1000)),
                new("maxSnipeExtensions", rule?.MaxSnipeExtensionsPerParticipant ?? 3)
            };

            // Khôi phục người đang dẫn đầu & leaderboard khi nạp lại cache giữa phiên
            var topAcceptedBid = auction.Bids?
                .Where(b => b.Status == BidStatus.ACCEPTED || b.Status == BidStatus.WINNING || b.Status == BidStatus.OUTBIB)
                .OrderByDescending(b => b.BidAmount)
                .FirstOrDefault();

            if (topAcceptedBid != null)
            {
                var topParticipant = auction.Participants?.FirstOrDefault(p => p.Id == topAcceptedBid.ParticipantId);
                hashEntries.Add(new("topBidderParticipantId", topAcceptedBid.ParticipantId.ToString()));
                if (topParticipant != null)
                {
                    hashEntries.Add(new("topBidderUserId", topParticipant.UserAccountId.ToString()));
                }

                var leaderboardKey = $"auction:{auction.Id}:leaderboard";
                var placedMs = new DateTimeOffset(topAcceptedBid.PlacedAt.ToUniversalTime()).ToUnixTimeMilliseconds();
                await db.SortedSetAddAsync(
                    leaderboardKey,
                    $"{topAcceptedBid.ParticipantId}:{topAcceptedBid.Id}:{placedMs}",
                    (double)topAcceptedBid.BidAmount);
            }

            await db.HashSetAsync(stateKey, hashEntries.ToArray());

            // Nạp thông tin người tham gia & số dư ví
            if (auction.Participants != null)
            {
                foreach (var p in auction.Participants)
                {
                    var initialDep = p.Deposits?.FirstOrDefault(d => d.DepositStatus == DepositStatus.LOCKED)?.DepositAmount ?? 0m;
                    await WriteParticipantAndWalletHashesAsync(db, auction.Id, p, initialDep, p.UserAccount?.Wallet);
                }
            }

            _logger.LogInformation(
                "[RedisCache] 🔥 Đã Warm-up phiên {AuctionId} ({Code}, Status={Status}, Participants={Count}) vào Redis RAM.",
                auction.Id, auction.AuctionCode, auction.AuctionStatus, participantCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[RedisCache] Lỗi Warm-up phiên {AuctionId}", auction.Id);
        }
    }

    public async Task<bool> IsAuctionWarmedUpAsync(
        Guid auctionId,
        bool requireActiveStatus = false,
        CancellationToken cancellationToken = default)
    {
        if (_redis == null || !_redis.IsConnected) return false;

        try
        {
            var db = _redis.GetDatabase();
            var status = await db.HashGetAsync($"auction:{auctionId}:state", "status");
            if (status.IsNullOrEmpty)
            {
                return false;
            }

            return !requireActiveStatus || string.Equals((string?)status, AuctionStatus.ACTIVE.ToString(), StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ActivateAuctionInCacheAsync(Guid auctionId, CancellationToken cancellationToken = default)
    {
        if (_redis == null || !_redis.IsConnected) return false;

        try
        {
            var db = _redis.GetDatabase();
            const string script = @"
                local status = redis.call('HGET', KEYS[1], 'status')
                if not status then
                    return 0
                end
                local pCount = tonumber(redis.call('HGET', KEYS[1], 'participantCount')) or 0
                if pCount <= 0 then
                    return 0
                end
                if status == 'SCHEDULE' then
                    redis.call('HSET', KEYS[1], 'status', 'ACTIVE')
                    return 1
                end
                if status == 'ACTIVE' then
                    return 1
                end
                return 0";

            var result = await db.ScriptEvaluateAsync(script, new RedisKey[] { $"auction:{auctionId}:state" });
            return !result.IsNull && (int)result == 1;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[RedisCache] Lỗi bật nhanh ACTIVE trên Redis cho phiên {AuctionId}", auctionId);
            return false;
        }
    }

    public async Task SyncParticipantRegistrationAsync(
        Guid auctionId,
        AuctionParticipant participant,
        decimal initialDeposit,
        Wallet? wallet,
        CancellationToken cancellationToken = default)
    {
        if (_redis == null || !_redis.IsConnected) return;

        try
        {
            var db = _redis.GetDatabase();
            var stateKey = $"auction:{auctionId}:state";

            await WriteParticipantAndWalletHashesAsync(db, auctionId, participant, initialDeposit, wallet);

            if (await db.KeyExistsAsync(stateKey))
            {
                await db.HashIncrementAsync(stateKey, "participantCount", 1);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[RedisCache] Lỗi đồng bộ participant {ParticipantId} vào Redis cho phiên {AuctionId}", participant.Id, auctionId);
        }
    }

    public async Task UpdateAuctionPriceInCacheAsync(
        Guid auctionId,
        decimal newPrice,
        long lockVersion,
        CancellationToken cancellationToken = default)
    {
        if (_redis == null || !_redis.IsConnected) return;

        try
        {
            var db = _redis.GetDatabase();
            var stateKey = $"auction:{auctionId}:state";
            if (!await db.KeyExistsAsync(stateKey)) return;

            await db.HashSetAsync(stateKey, new HashEntry[]
            {
                new("currentPrice", (long)newPrice),
                new("lockVersion", lockVersion)
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[RedisCache] Lỗi cập nhật giá Redis cho phiên {AuctionId}", auctionId);
        }
    }

    public async Task<DateTime?> GetCachedEndAtUtcAsync(Guid auctionId, CancellationToken cancellationToken = default)
    {
        if (_redis == null || !_redis.IsConnected) return null;

        try
        {
            var db = _redis.GetDatabase();
            var val = await db.HashGetAsync($"auction:{auctionId}:state", "endAtMs");
            if (val.HasValue && long.TryParse((string?)val, out var endAtMs) && endAtMs > 0)
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(endAtMs).UtcDateTime;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[RedisCache] Lỗi đọc endAtMs từ Redis cho phiên {AuctionId}", auctionId);
        }

        return null;
    }

    public async Task<LuaBidResult> PlaceBidAtomicAsync(
        Guid auctionId,
        Guid participantId,
        Guid userId,
        decimal bidAmount,
        Guid bidId,
        CancellationToken cancellationToken = default)
    {
        if (_redis == null || !_redis.IsConnected)
        {
            return new LuaBidResult(false, "ERR_REDIS_UNAVAILABLE", "Redis In-Memory không khả dụng.", bidId, auctionId, participantId, userId, bidAmount, 0, 0, false, 0);
        }

        try
        {
            var db = _redis.GetDatabase();
            var script = LoadLuaScript();

            var keys = new RedisKey[]
            {
                $"auction:{auctionId}:state",
                $"auction:{auctionId}:leaderboard",
                $"auction:{auctionId}:participant:{participantId}",
                $"wallet:{userId}:balance"
            };

            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var args = new RedisValue[]
            {
                participantId.ToString(),
                userId.ToString(),
                Convert.ToInt64(bidAmount),
                nowMs,
                bidId.ToString(),
                auctionId.ToString()
            };

            var rawResult = await db.ScriptEvaluateAsync(script, keys, args);
            if (rawResult.IsNull)
            {
                return new LuaBidResult(false, "ERR_EMPTY_RESPONSE", "Không nhận được phản hồi từ Redis Lua Script.", bidId, auctionId, participantId, userId, bidAmount, 0, 0, false, 0);
            }

            var json = (string?)rawResult;
            if (string.IsNullOrWhiteSpace(json))
            {
                return new LuaBidResult(false, "ERR_INVALID_JSON", "Phản hồi Lua Script không hợp lệ.", bidId, auctionId, participantId, userId, bidAmount, 0, 0, false, 0);
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var success = root.GetProperty("success").GetBoolean();

            if (!success)
            {
                var errCode = root.TryGetProperty("errorCode", out var ec) ? ec.GetString() : "ERR_BID_REJECTED";
                var msg = root.TryGetProperty("message", out var m) ? m.GetString() : "Lượt đặt giá bị từ chối.";
                return new LuaBidResult(false, errCode, msg, bidId, auctionId, participantId, userId, bidAmount, 0, 0, false, 0);
            }

            var newCurrentPrice = root.GetProperty("newCurrentPrice").GetDecimal();
            var newEndAtMs = root.GetProperty("newEndAtMs").GetInt64();
            var wasExtended = root.TryGetProperty("wasExtended", out var we) && we.GetBoolean();
            var lockVersion = root.GetProperty("lockVersion").GetInt64();
            var justActivated = root.TryGetProperty("justActivated", out var ja) && ja.GetBoolean();

            return new LuaBidResult(true, null, null, bidId, auctionId, participantId, userId, bidAmount, newCurrentPrice, newEndAtMs, wasExtended, lockVersion, justActivated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RedisCache] Lỗi thực thi Lua Script cho phiên {AuctionId}", auctionId);
            return new LuaBidResult(false, "ERR_SCRIPT_EXCEPTION", ex.Message, bidId, auctionId, participantId, userId, bidAmount, 0, 0, false, 0);
        }
    }

    public async Task<IReadOnlyList<LeaderboardTopEntry>> GetTopBiddersAsync(Guid auctionId, int count = 2, CancellationToken cancellationToken = default)
    {
        var results = new List<LeaderboardTopEntry>();
        if (_redis == null || !_redis.IsConnected) return results;

        try
        {
            var db = _redis.GetDatabase();
            var zsetKey = $"auction:{auctionId}:leaderboard";

            var entries = await db.SortedSetRangeByRankWithScoresAsync(zsetKey, 0, count - 1, Order.Descending);
            foreach (var item in entries)
            {
                var memberStr = (string?)item.Element;
                if (string.IsNullOrWhiteSpace(memberStr)) continue;

                var parts = memberStr.Split(':');
                if (parts.Length >= 3 && Guid.TryParse(parts[0], out var pId) && Guid.TryParse(parts[1], out var bId) && long.TryParse(parts[2], out var ts))
                {
                    results.Add(new LeaderboardTopEntry(pId, bId, (decimal)item.Score, ts));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[RedisCache] Lỗi lấy Leaderboard Top cho phiên {AuctionId}", auctionId);
        }

        return results;
    }

    public async Task SyncWalletBalanceAsync(Guid userId, decimal availableBalance, decimal lockedBalance, CancellationToken cancellationToken = default)
    {
        if (_redis == null || !_redis.IsConnected) return;

        try
        {
            var db = _redis.GetDatabase();
            var wKey = $"wallet:{userId}:balance";
            await db.HashSetAsync(wKey, new HashEntry[]
            {
                new("availableBalance", Convert.ToInt64(availableBalance)),
                new("lockedBalance", Convert.ToInt64(lockedBalance))
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[RedisCache] Lỗi đồng bộ số dư ví {UserId} lên Redis.", userId);
        }
    }

    public async Task EvictAuctionCacheWithTtlAsync(Guid auctionId, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        if (_redis == null || !_redis.IsConnected) return;

        try
        {
            var db = _redis.GetDatabase();
            var keys = new RedisKey[]
            {
                $"auction:{auctionId}:state",
                $"auction:{auctionId}:leaderboard"
            };

            foreach (var k in keys)
            {
                await db.KeyExpireAsync(k, ttl);
            }

            _logger.LogInformation("[RedisCache] ⏳ Đã thiết lập TTL {TtlHours}h cho phiên {AuctionId}", ttl.TotalHours, auctionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[RedisCache] Lỗi gắn TTL cho phiên {AuctionId}", auctionId);
        }
    }

    public async Task InvalidateAuctionCacheAsync(Guid auctionId, CancellationToken cancellationToken = default)
    {
        if (_redis == null || !_redis.IsConnected) return;

        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(new RedisKey[]
            {
                $"auction:{auctionId}:state",
                $"auction:{auctionId}:leaderboard"
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[RedisCache] Lỗi xóa cache Redis cho phiên {AuctionId}", auctionId);
        }
    }

    private static async Task WriteParticipantAndWalletHashesAsync(
        IDatabase db,
        Guid auctionId,
        AuctionParticipant participant,
        decimal initialDeposit,
        Wallet? wallet)
    {
        var pKey = $"auction:{auctionId}:participant:{participant.Id}";
        await db.HashSetAsync(pKey, new HashEntry[]
        {
            new("userId", participant.UserAccountId.ToString()),
            new("status", participant.Status.ToString()),
            new("initialDeposit", (long)initialDeposit),
            new("acceptedBidCount", participant.AcceptedBidCount),
            new("snipeExtensionCount", participant.SnipeExtensionCount)
        });

        if (wallet != null)
        {
            var wKey = $"wallet:{participant.UserAccountId}:balance";
            await db.HashSetAsync(wKey, new HashEntry[]
            {
                new("availableBalance", (long)wallet.AvailableBalance),
                new("lockedBalance", (long)wallet.LockedBalance),
                new("status", wallet.WalletStatus.ToString())
            });
        }
    }
}
