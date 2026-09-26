using TrustSimBackend.Application.Features.Auction.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Auction.Hubs;

/// <summary>
/// Service trung gian thông báo sự kiện đấu giá theo thời gian thực (SignalR Abstraction).
/// </summary>
public interface IAuctionRealtimeNotifier
{
    /// <summary>
    /// Bắn thông báo lượt trả giá mới tới tất cả client đang xem phiên đấu giá.
    /// </summary>
    Task NotifyNewBidAsync(Guid auctionId, BidDto bid, decimal newCurrentPrice, int totalBids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bắn thông báo gia hạn Anti-Sniping phút chót tới tất cả client đang xem phiên đấu giá.
    /// </summary>
    Task NotifyAuctionExtendedAsync(Guid auctionId, DateTime newEndAt, int extensionSeconds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bắn thông báo trạng thái phiên thay đổi (Bắt đầu, Tạm dừng, Kết thúc).
    /// </summary>
    Task NotifyAuctionStatusChangedAsync(Guid auctionId, string status, Guid? winnerId, decimal? finalPrice, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bắn thông báo có phiên đấu giá mới được tạo/duyệt tới group danh sách phiên (/auctions).
    /// </summary>
    Task NotifyNewAuctionCreatedAsync(AuctionDto auction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bắn thông báo nhịp giảm giá mới trong đấu giá kiểu Hà Lan.
    /// </summary>
    Task NotifyPriceDropAsync(Guid auctionId, decimal currentPrice, int nextDropInSeconds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bắn thông báo kết thúc phiên đấu giá và kết quả chốt phiên.
    /// </summary>
    Task NotifyAuctionEndedAsync(Guid auctionId, AuctionResultDto result, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bắn thông báo phiên đấu giá thất bại (không ai mua, bùng cọc hoặc chạm sàn).
    /// </summary>
    Task NotifyAuctionFailedAsync(Guid auctionId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bắn thông báo nội bộ trong phòng đấu giá.
    /// </summary>
    Task NotifyAuctionNotificationAsync(Guid auctionId, string message, string type, CancellationToken cancellationToken = default);
}
