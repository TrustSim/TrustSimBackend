using Microsoft.AspNetCore.SignalR;

namespace TrustSimBackend.API.Hubs;

/// <summary>
/// SignalR Hub điều phối kết nối WebSocket thời gian thực cho hệ thống Đấu giá TrustSim.
/// </summary>
public class AuctionHub : Hub<IAuctionHubClient>
{
    private readonly ILogger<AuctionHub> _logger;

    public AuctionHub(ILogger<AuctionHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Tham gia phòng theo dõi phiên đấu giá cụ thể theo auctionId.
    /// </summary>
    public async Task JoinAuctionGroup(string auctionId)
    {
        if (string.IsNullOrWhiteSpace(auctionId)) return;
        var groupName = $"auction_{auctionId.Trim().ToLower()}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("[AuctionHub] Client {ConnectionId} tham gia nhóm {GroupName}", Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Rời phòng theo dõi phiên đấu giá khi người dùng rời khỏi trang chi tiết.
    /// </summary>
    public async Task LeaveAuctionGroup(string auctionId)
    {
        if (string.IsNullOrWhiteSpace(auctionId)) return;
        var groupName = $"auction_{auctionId.Trim().ToLower()}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("[AuctionHub] Client {ConnectionId} rời nhóm {GroupName}", Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Tham gia phòng theo dõi danh sách tất cả các phiên đấu giá (/auctions).
    /// </summary>
    public async Task JoinListingFeed()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "auctions_listing");
        _logger.LogInformation("[AuctionHub] Client {ConnectionId} tham gia danh sách auctions_listing", Context.ConnectionId);
    }

    /// <summary>
    /// Rời phòng theo dõi danh sách.
    /// </summary>
    public async Task LeaveListingFeed()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "auctions_listing");
        _logger.LogInformation("[AuctionHub] Client {ConnectionId} rời danh sách auctions_listing", Context.ConnectionId);
    }

    /// <summary>
    /// Tham gia phòng nhận thông báo cá nhân theo userId.
    /// </summary>
    public async Task JoinPersonalNotificationFeed(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;
        var groupName = $"user_{userId.Trim().ToLower()}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("[AuctionHub] Client {ConnectionId} tham gia hòm thư cá nhân {GroupName}", Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Rời phòng nhận thông báo cá nhân.
    /// </summary>
    public async Task LeavePersonalNotificationFeed(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;
        var groupName = $"user_{userId.Trim().ToLower()}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("[AuctionHub] Client {ConnectionId} rời hòm thư cá nhân {GroupName}", Context.ConnectionId, groupName);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("[AuctionHub] Client kết nối: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("[AuctionHub] Client ngắt kết nối: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
