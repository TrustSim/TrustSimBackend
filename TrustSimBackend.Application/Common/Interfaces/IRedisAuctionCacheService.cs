using TrustSimBackend.Domain.Entities.Auctions;

namespace TrustSimBackend.Application.Common.Interfaces;

public record LuaBidResult(
    bool Success,
    string? ErrorCode,
    string? Message,
    Guid BidId,
    Guid AuctionId,
    Guid ParticipantId,
    Guid UserId,
    decimal BidAmount,
    decimal NewCurrentPrice,
    long NewEndAtMs,
    bool WasExtended,
    long LockVersion);

public record LeaderboardTopEntry(
    Guid ParticipantId,
    Guid BidId,
    decimal BidAmount,
    long PlacedAtMs);

/// <summary>
/// Dịch vụ Hot Cache điều phối bộ nhớ đệm Redis In-Memory và thực thi Lua Script nguyên tử (<1ms).
/// </summary>
public interface IRedisAuctionCacheService
{
    /// <summary>
    /// Nạp trước toàn bộ dữ liệu phiên, quy tắc, người tham gia và số dư ví vào Redis RAM khi mở phòng.
    /// </summary>
    Task WarmUpAuctionAsync(Auction auction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Thực thi Lua Script đặt giá nguyên tử siêu tốc (<1ms) trên Redis.
    /// </summary>
    Task<LuaBidResult> PlaceBidAtomicAsync(
        Guid auctionId,
        Guid participantId,
        Guid userId,
        decimal bidAmount,
        Guid bidId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách Top N người đặt giá cao nhất từ Redis ZSET (Top 1 = Winner, Top 2 = Runner-Up).
    /// </summary>
    Task<IReadOnlyList<LeaderboardTopEntry>> GetTopBiddersAsync(Guid auctionId, int count = 2, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cập nhật số dư khả dụng ví người dùng trên Redis Cache.
    /// </summary>
    Task SyncWalletBalanceAsync(Guid userId, decimal availableBalance, decimal lockedBalance, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gắn TTL và dọn dẹp các Key phiên sau khi hoàn tất quyết toán.
    /// </summary>
    Task EvictAuctionCacheWithTtlAsync(Guid auctionId, TimeSpan ttl, CancellationToken cancellationToken = default);
}
