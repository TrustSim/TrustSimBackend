using TrustSimBackend.Application.Features.Chat.DTOs.Responses;

namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Dịch vụ phát thông báo thời gian thực (SignalR Abstraction) cho hệ thống Chat.
/// Đảm bảo phân phối sự kiện đúng đối tượng và tuân thủ tuyệt đối quyền riêng tư khi Support Chat đã được duyệt.
/// </summary>
public interface IChatRealtimeService
{
    /// <summary>
    /// Phát tin nhắn mới và cập nhật tóm tắt hội thoại tới đúng những người có quyền trong cuộc trò chuyện.
    /// </summary>
    Task NotifyNewMessageAsync(
        ChatConversationSummaryDto conversationForInitiator,
        ChatConversationSummaryDto? conversationForTargetOrQueue,
        ChatMessageDto message,
        Guid initiatorUserId,
        Guid? targetUserId,
        bool broadcastToSupportQueue,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phát thông báo có cuộc hội thoại hỗ trợ mới đang chờ (PENDING) tới hàng đợi Nhân viên Sàn / Admin.
    /// </summary>
    Task NotifyNewPendingSupportConversationAsync(
        ChatConversationSummaryDto conversation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phát thông báo cuộc hội thoại hỗ trợ đã được 1 Nhân viên Sàn hoặc Admin duyệt nhận.
    /// Giúp giao diện của tất cả các Nhân viên Sàn và Admin khác lập tức xóa/ẩn cuộc hội thoại này khỏi danh sách chờ.
    /// </summary>
    Task NotifySupportConversationClaimedAsync(
        Guid conversationId,
        Guid initiatorUserId,
        Guid claimedByStaffId,
        string claimedByStaffName,
        DateTime claimedAt,
        ChatConversationSummaryDto updatedForInitiator,
        ChatConversationSummaryDto updatedForClaimedStaff,
        ChatMessageDto systemMessage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phát thông báo đối phương đã đọc tin nhắn trong cuộc hội thoại.
    /// </summary>
    Task NotifyMessagesReadAsync(
        Guid conversationId,
        Guid readerUserId,
        Guid? notifyTargetUserId,
        DateTime readAt,
        CancellationToken cancellationToken = default);
}
