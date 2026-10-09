using TrustSimBackend.Domain.Entities.System;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Dịch vụ Điều phối Thông báo Đa kênh (Multi-Channel Notification Dispatcher).
/// Chịu trách nhiệm lưu CSDL In-App, phát WebSocket SignalR Real-time và gửi Email tự động.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Gửi thông báo đến người dùng cụ thể: Lưu CSDL + Push SignalR vào hòm thư cá nhân (user_{userId}) + Gửi Email (nếu bật).
    /// </summary>
    Task<Notification> SendNotificationAsync(
        Guid userId,
        NotificationType type,
        string title,
        string content,
        string? actionUrl = null,
        string? referenceType = null,
        Guid? referenceId = null,
        object? metadata = null,
        bool sendEmail = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phát thông báo nội bộ tới những người tham gia và theo dõi cùng 1 phòng đấu giá (Room: auction_{auctionId}).
    /// Không gửi toàn hệ thống, bảo đảm tính riêng tư và đúng ngữ cảnh phòng đấu giá.
    /// </summary>
    Task SendAuctionRoomNotificationAsync(
        Guid auctionId,
        string message,
        string type = "INFO",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phát sóng thông báo toàn hệ thống (System Broadcast).
    /// </summary>
    Task BroadcastNotificationAsync(
        string title,
        string content,
        string? actionUrl = null,
        CancellationToken cancellationToken = default);
}
