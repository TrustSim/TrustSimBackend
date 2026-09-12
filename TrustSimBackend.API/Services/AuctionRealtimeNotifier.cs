using Microsoft.AspNetCore.SignalR;
using TrustSimBackend.API.Hubs;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;
using TrustSimBackend.Application.Features.Auction.Hubs;

namespace TrustSimBackend.API.Services;

/// <summary>
/// Cài đặt service phát thông báo thời gian thực qua SignalR Hub tới các phòng tương ứng.
/// </summary>
public class AuctionRealtimeNotifier : IAuctionRealtimeNotifier
{
    private readonly IHubContext<AuctionHub, IAuctionHubClient> _hubContext;
    private readonly ILogger<AuctionRealtimeNotifier> _logger;

    public AuctionRealtimeNotifier(
        IHubContext<AuctionHub, IAuctionHubClient> hubContext,
        ILogger<AuctionRealtimeNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyNewBidAsync(Guid auctionId, BidDto bid, decimal newCurrentPrice, int totalBids, CancellationToken cancellationToken = default)
    {
        var groupName = $"auction_{auctionId.ToString().ToLower()}";
        _logger.LogInformation("[Realtime] Phát sóng NewBid ({BidAmount:N0} VNĐ) tới nhóm {GroupName}", bid.BidAmount, groupName);
        await _hubContext.Clients.Group(groupName).ReceiveNewBid(bid, newCurrentPrice, totalBids);
    }

    public async Task NotifyAuctionExtendedAsync(Guid auctionId, DateTime newEndAt, int extensionSeconds, CancellationToken cancellationToken = default)
    {
        var groupName = $"auction_{auctionId.ToString().ToLower()}";
        _logger.LogInformation("[Realtime] Phát sóng Anti-Sniping (+{ExtensionSeconds}s) tới nhóm {GroupName}", extensionSeconds, groupName);
        await _hubContext.Clients.Group(groupName).ReceiveAuctionExtended(newEndAt, extensionSeconds);
    }

    public async Task NotifyAuctionStatusChangedAsync(Guid auctionId, string status, Guid? winnerId, decimal? finalPrice, CancellationToken cancellationToken = default)
    {
        var groupName = $"auction_{auctionId.ToString().ToLower()}";
        _logger.LogInformation("[Realtime] Phát sóng StatusChanged ({Status}) tới nhóm {GroupName}", status, groupName);
        await _hubContext.Clients.Group(groupName).ReceiveAuctionStatusChanged(auctionId, status, winnerId, finalPrice);
    }

    public async Task NotifyNewAuctionCreatedAsync(AuctionDto auction, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Realtime] Phát sóng NewAuctionCreated ({AuctionCode}) tới danh sách auctions_listing", auction.AuctionCode);
        await _hubContext.Clients.Group("auctions_listing").ReceiveNewAuctionCreated(auction);
    }
}
