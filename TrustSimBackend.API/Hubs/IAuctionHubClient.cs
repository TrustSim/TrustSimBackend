using TrustSimBackend.Application.Features.Auction.DTOs.Responses;

namespace TrustSimBackend.API.Hubs;

/// <summary>
/// Strongly-typed client methods mà Server có thể gọi xuống Client thông qua SignalR.
/// </summary>
public interface IAuctionHubClient
{
    /// <summary>
    /// Nhận thông báo có lượt trả giá mới trong phiên.
    /// </summary>
    Task ReceiveNewBid(BidDto bid, decimal newCurrentPrice, int totalBids);

    /// <summary>
    /// Nhận thông báo gia hạn Anti-Sniping giờ chót.
    /// </summary>
    Task ReceiveAuctionExtended(DateTime newEndAt, int extensionSeconds);

    /// <summary>
    /// Nhận thông báo trạng thái phiên thay đổi.
    /// </summary>
    Task ReceiveAuctionStatusChanged(Guid auctionId, string status, Guid? winnerId, decimal? finalPrice);

    /// <summary>
    /// Nhận thông báo có phiên đấu giá mới được tạo/duyệt.
    /// </summary>
    Task ReceiveNewAuctionCreated(AuctionDto auction);
}
