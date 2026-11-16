using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Entities.Financial;

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
    long LockVersion,
    bool JustActivated = false);

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
    /// Nạp trước toàn bộ dữ liệu phiên, quy tắc, người tham gia và số dư ví vào Redis RAM.
    /// </summary>
    Task WarmUpAuctionAsync(Auction auction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra xem phiên đấu giá đã tồn tại trên Redis RAM (và đúng trạng thái nếu yêu cầu) hay chưa.
    /// </summary>
    Task<bool> IsAuctionWarmedUpAsync(Guid auctionId, bool requireActiveStatus = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bật nhanh trạng thái phiên từ SCHEDULE sang ACTIVE trên Redis RAM (dùng sau bước Pre-Warm).
    /// Trả về false nếu phiên chưa có trên Redis hoặc chưa có người tham gia để caller fallback sang WarmUp đầy đủ.
    /// </summary>
    Task<bool> ActivateAuctionInCacheAsync(Guid auctionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đồng bộ người tham gia mới đặt cọc và số dư ví của họ vào Redis RAM nếu phiên đã được Pre-Warm.
    /// </summary>
    Task SyncParticipantRegistrationAsync(
        Guid auctionId,
        AuctionParticipant participant,
        decimal initialDeposit,
        Wallet? wallet,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cập nhật giá hiện tại của phiên Hà Lan trên Redis Cache.
    /// </summary>
    Task UpdateAuctionPriceInCacheAsync(
        Guid auctionId,
        decimal newPrice,
        long lockVersion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy mốc thời gian kết thúc phiên (EndAt UTC) hiện đang lưu trên Redis nếu tồn tại (bao gồm Anti-Sniping).
    /// </summary>
    Task<DateTime?> GetCachedEndAtUtcAsync(Guid auctionId, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Xóa ngay key trạng thái phiên trên Redis (khi phiên bị hủy/đóng).
    /// </summary>
    Task InvalidateAuctionCacheAsync(Guid auctionId, CancellationToken cancellationToken = default);
}
