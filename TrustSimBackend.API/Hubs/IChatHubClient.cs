using TrustSimBackend.Application.Features.Chat.DTOs.Responses;

namespace TrustSimBackend.API.Hubs;

/// <summary>
/// Strongly-typed SignalR Client methods cho hệ thống Chat thời gian thực (Direct &amp; Support).
/// </summary>
public interface IChatHubClient
{
    /// <summary>
    /// Nhận tin nhắn mới trong cuộc hội thoại.
    /// </summary>
    Task ReceiveChatMessage(ChatMessageDto message);

    /// <summary>
    /// Cập nhật thông tin tóm tắt của cuộc hội thoại trên danh sách hội thoại.
    /// </summary>
    Task ReceiveConversationUpdated(ChatConversationSummaryDto conversation);

    /// <summary>
    /// [Dành cho Staff/Admin trong hàng đợi support_queue]
    /// Nhận thông báo có cuộc hội thoại hỗ trợ mới đang ở trạng thái chờ (PENDING).
    /// </summary>
    Task ReceiveNewPendingSupportConversation(ChatConversationSummaryDto conversation);

    /// <summary>
    /// [Dành cho Staff/Admin trong hàng đợi support_queue]
    /// Nhận thông báo cuộc hội thoại hỗ trợ đã được một Nhân viên Sàn hoặc Admin duyệt nhận.
    /// Giao diện của các Nhân viên Sàn và Admin khác lập tức xóa/ẩn cuộc hội thoại này khỏi danh sách chờ.
    /// </summary>
    Task ReceiveSupportConversationClaimed(
        Guid conversationId,
        Guid claimedByStaffId,
        string claimedByStaffName,
        DateTime claimedAt);

    /// <summary>
    /// Nhận thông báo đối phương đã xem tin nhắn trong cuộc hội thoại.
    /// </summary>
    Task ReceiveMessagesRead(Guid conversationId, Guid readerUserId, DateTime readAt);
}
