using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Dịch vụ Hot-Cache In-Memory Redis quản lý trạng thái phiên, ZSET Leaderboard và thực thi Lua Script nguyên tử.
/// </summary>
public class RedisAuctionCacheService : IRedisAuctionCacheService
{
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

    private string LoadLuaScript()
    {
        if (_cachedLuaScript != null) return _cachedLuaScript;
        lock (_scriptLock)
        {
            if (_cachedLuaScript != null) return _cachedLuaScript;
            var scriptPath = Path.Combine(AppContext.BaseDirectory, "Scripts", "place_bid.lua");
            if (File.Exists(scriptPath))
            {
                _cachedLuaScript = File.ReadAllText(scriptPath);
            }
            else
            {
                // Fallback nhúng trực tiếp nếu file build chưa copy ra output folder
                _cachedLuaScript = @"
                    local status = redis.call('HGET', KEYS[1], 'status')
                    if not status or status ~= 'ACTIVE' then
                        return cjson.encode({ success = false, errorCode = 'ERR_AUCTION_NOT_ACTIVE', message = 'Phiên đấu giá chưa mở hoặc đã kết thúc.' })
                    end
                    local endAtMs = tonumber(redis.call('HGET', KEYS[1], 'endAtMs')) or 0
                    local nowMs = tonumber(ARGV[4])
                    if nowMs >= endAtMs then
                        return cjson.encode({ success = false, errorCode = 'ERR_AUCTION_ENDED', message = 'Đã hết thời gian đặt giá.' })
                    end
                    local pStatus = redis.call('HGET', KEYS[3], 'status')
                    if not pStatus or pStatus == 'BANNED' then
                        return cjson.encode({ success = false, errorCode = 'ERR_PARTICIPANT_BANNED', message = 'Bạn không có quyền tham gia trả giá trong phiên này.' })
                    end
                    local topBidderParticipantId = redis.call('HGET', KEYS[1], 'topBidderParticipantId')
                    if topBidderParticipantId == ARGV[1] then
                        return cjson.encode({ success = false, errorCode = 'ERR_SELF_OUTBID', message = 'Bạn đang là người giữ mức giá cao nhất hiện tại.' })
                    end
                    local currentPrice = tonumber(redis.call('HGET', KEYS[1], 'currentPrice')) or 0
                    local minIncrement = tonumber(redis.call('HGET', KEYS[1], 'minIncrement')) or 100000
                    local maxMultiplier = tonumber(redis.call('HGET', KEYS[1], 'maxMultiplier')) or 10
                    local bidAmount = tonumber(ARGV[3])
                    local minRequired = currentPrice + minIncrement
                    local maxAllowed = currentPrice + (maxMultiplier * minIncrement)
                    if bidAmount < minRequired then
                        return cjson.encode({ success = false, errorCode = 'ERR_BID_TOO_LOW', message = 'Giá đặt phải lớn hơn hoặc bằng ' .. tostring(minRequired) .. ' VNĐ.' })
                    end
                    if bidAmount > maxAllowed then
                        return cjson.encode({ success = false, errorCode = 'ERR_BID_EXCEEDS_MAX_STEP', message = 'Giá đặt vượt quá mức tăng tối đa cho phép (' .. tostring(maxAllowed) .. ' VNĐ).' })
                    end
                    local marginRate = tonumber(redis.call('HGET', KEYS[1], 'marginRate')) or 0.10
                    local initialDeposit = tonumber(redis.call('HGET', KEYS[3], 'initialDeposit')) or 0
                    local requiredMarginA = bidAmount * marginRate
                    local deltaMargin = requiredMarginA - initialDeposit
                    local walletBalance = tonumber(redis.call('HGET', KEYS[4], 'availableBalance')) or 0
                    if deltaMargin > 0 and walletBalance < deltaMargin then
                        return cjson.encode({ success = false, errorCode = 'ERR_INSUFFICIENT_MARGIN', message = 'Số dư ví không đủ đáp ứng ký quỹ ' .. tostring(requiredMarginA) .. ' VNĐ. Cần bổ sung thêm ' .. tostring(deltaMargin) .. ' VNĐ.' })
                    end
                    local wasExtended = 0
                    local extensionThresholdMs = tonumber(redis.call('HGET', KEYS[1], 'extensionThresholdMs')) or 30000
                    local extensionDurationMs = tonumber(redis.call('HGET', KEYS[1], 'extensionDurationMs')) or 30000
                    local maxSnipeExtensions = tonumber(redis.call('HGET', KEYS[1], 'maxSnipeExtensions')) or 3
                    local currentSnipeCount = tonumber(redis.call('HGET', KEYS[3], 'snipeExtensionCount')) or 0
                    local remainingMs = endAtMs - nowMs
                    if remainingMs > 0 and remainingMs <= extensionThresholdMs and currentSnipeCount < maxSnipeExtensions then
                        endAtMs = endAtMs + extensionDurationMs
                        redis.call('HSET', KEYS[1], 'endAtMs', endAtMs)
                        redis.call('HINCRBY', KEYS[3], 'snipeExtensionCount', 1)
                        wasExtended = 1
                    end
                    redis.call('HSET', KEYS[1], 'currentPrice', bidAmount)
                    redis.call('HSET', KEYS[1], 'topBidderParticipantId', ARGV[1])
                    redis.call('HSET', KEYS[1], 'topBidderUserId', ARGV[2])
                    local newLockVersion = redis.call('HINCRBY', KEYS[1], 'lockVersion', 1)
                    local zsetMember = ARGV[1] .. ':' .. ARGV[5] .. ':' .. ARGV[4]
                    redis.call('ZADD', KEYS[2], bidAmount, zsetMember)
                    redis.call('HINCRBY', KEYS[3], 'acceptedBidCount', 1)
                    redis.call('HSET', KEYS[3], 'lastBidTimeMs', ARGV[4])
                    local streamPayload = cjson.encode({ bidId = ARGV[5], auctionId = ARGV[6], participantId = ARGV[1], userId = ARGV[2], bidAmount = bidAmount, placedAtMs = nowMs, isSnipeExtended = (wasExtended == 1), newEndAtMs = endAtMs, lockVersion = newLockVersion })
                    redis.call('XADD', 'stream:auction:bids', '*', 'payload', streamPayload)
                    return cjson.encode({ success = true, bidId = ARGV[5], auctionId = ARGV[6], participantId = ARGV[1], userId = ARGV[2], bidAmount = bidAmount, newCurrentPrice = bidAmount, newEndAtMs = endAtMs, wasExtended = (wasExtended == 1), lockVersion = newLockVersion })
                ";
            }
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

            var endAtMs = new DateTimeOffset(auction.EndAt.ToUniversalTime()).ToUnixTimeMilliseconds();

            var hashEntries = new HashEntry[]
            {
                new("auctionId", auction.Id.ToString()),
                new("status", auction.AuctionStatus.ToString()),
                new("currentPrice", (long)auction.CurrentPrice),
                new("startingPrice", (long)auction.StartingPrice),
                new("minIncrement", (long)(rule?.MinimumBidIncrement ?? 100000)),
                new("maxMultiplier", rule?.MaxStepMultiplier ?? 10),
                new("marginRate", (double)(rule?.MarginPercentage ?? 0.10m)),
                new("endAtMs", endAtMs),
                new("lockVersion", (long)auction.LockVersion),
                new("extensionThresholdMs", (long)((rule?.ExtensionThresholdSeconds ?? 30) * 1000)),
                new("extensionDurationMs", (long)((rule?.ExtensionDurationSeconds ?? 30) * 1000)),
                new("maxSnipeExtensions", rule?.MaxSnipeExtensionsPerParticipant ?? 3)
            };

            await db.HashSetAsync(stateKey, hashEntries);

            // Nạp thông tin người tham gia & số dư ví
            if (auction.Participants != null)
            {
                foreach (var p in auction.Participants)
                {
                    var pKey = $"auction:{auction.Id}:participant:{p.Id}";
                    var initialDep = p.Deposits?.FirstOrDefault(d => d.DepositStatus == DepositStatus.LOCKED)?.DepositAmount ?? 0m;

                    await db.HashSetAsync(pKey, new HashEntry[]
                    {
                        new("userId", p.UserAccountId.ToString()),
                        new("status", p.Status.ToString()),
                        new("initialDeposit", (long)initialDep),
                        new("acceptedBidCount", p.AcceptedBidCount),
                        new("snipeExtensionCount", p.SnipeExtensionCount)
                    });

                    if (p.UserAccount?.Wallet != null)
                    {
                        var wKey = $"wallet:{p.UserAccountId}:balance";
                        await db.HashSetAsync(wKey, new HashEntry[]
                        {
                            new("availableBalance", (long)p.UserAccount.Wallet.AvailableBalance),
                            new("lockedBalance", (long)p.UserAccount.Wallet.LockedBalance),
                            new("status", p.UserAccount.Wallet.WalletStatus.ToString())
                        });
                    }
                }
            }

            _logger.LogInformation("[RedisCache] 🔥 Đã Warm-up thành công phiên {AuctionId} ({Code}) vào Redis RAM.", auction.Id, auction.AuctionCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[RedisCache] Lỗi Warm-up phiên {AuctionId}", auction.Id);
        }
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

            return new LuaBidResult(true, null, null, bidId, auctionId, participantId, userId, bidAmount, newCurrentPrice, newEndAtMs, wasExtended, lockVersion);
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
}
