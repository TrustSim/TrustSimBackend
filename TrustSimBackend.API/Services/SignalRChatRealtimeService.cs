using Microsoft.AspNetCore.SignalR;
using TrustSimBackend.API.Hubs;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Chat.DTOs.Responses;

namespace TrustSimBackend.API.Services;

/// <summary>
/// Triển khai dịch vụ phát sự kiện Chat thời gian thực qua SignalR ChatHub.
/// Đảm bảo khi cuộc hội thoại Support đã được duyệt (CLAIMED), sự kiện chỉ gửi riêng cho khách hàng và nhân viên đã duyệt.
/// </summary>
public class SignalRChatRealtimeService : IChatRealtimeService
{
    private readonly IHubContext<ChatHub, IChatHubClient> _hubContext;
    private readonly ILogger<SignalRChatRealtimeService> _logger;

    public SignalRChatRealtimeService(
        IHubContext<ChatHub, IChatHubClient> hubContext,
        ILogger<SignalRChatRealtimeService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyNewMessageAsync(
        ChatConversationSummaryDto conversationForInitiator,
        ChatConversationSummaryDto? conversationForTargetOrQueue,
        ChatMessageDto message,
        Guid initiatorUserId,
        Guid? targetUserId,
        bool broadcastToSupportQueue,
        CancellationToken cancellationToken = default)
    {
        var initiatorGroup = ChatHub.GetUserGroupName(initiatorUserId);
        await _hubContext.Clients.Group(initiatorGroup).ReceiveChatMessage(message);
        await _hubContext.Clients.Group(initiatorGroup).ReceiveConversationUpdated(conversationForInitiator);

        if (targetUserId.HasValue && targetUserId.Value != Guid.Empty && targetUserId.Value != initiatorUserId)
        {
            var targetGroup = ChatHub.GetUserGroupName(targetUserId.Value);
            await _hubContext.Clients.Group(targetGroup).ReceiveChatMessage(message);
            if (conversationForTargetOrQueue != null)
            {
                await _hubContext.Clients.Group(targetGroup).ReceiveConversationUpdated(conversationForTargetOrQueue);
            }
        }
        else if (broadcastToSupportQueue && conversationForTargetOrQueue != null)
        {
            // Chỉ khi cuộc hội thoại SUPPORT đang ở trạng thái PENDING (chưa ai nhận) mới phát vào hàng đợi chung
            await _hubContext.Clients.Group(ChatHub.SupportQueueGroupName).ReceiveChatMessage(message);
            await _hubContext.Clients.Group(ChatHub.SupportQueueGroupName).ReceiveConversationUpdated(conversationForTargetOrQueue);
        }
    }

    public async Task NotifyNewPendingSupportConversationAsync(
        ChatConversationSummaryDto conversation,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[ChatRealtime] Phát thông báo cuộc hội thoại hỗ trợ mới {ConversationId} tới hàng đợi {SupportQueue}",
            conversation.Id, ChatHub.SupportQueueGroupName);

        await _hubContext.Clients.Group(ChatHub.SupportQueueGroupName).ReceiveNewPendingSupportConversation(conversation);
    }

    public async Task NotifySupportConversationClaimedAsync(
        Guid conversationId,
        Guid initiatorUserId,
        Guid claimedByStaffId,
        string claimedByStaffName,
        DateTime claimedAt,
        ChatConversationSummaryDto updatedForInitiator,
        ChatConversationSummaryDto updatedForClaimedStaff,
        ChatMessageDto systemMessage,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[ChatRealtime] Cuộc hội thoại hỗ trợ {ConversationId} đã được duyệt bởi {StaffName} ({StaffId}) -> Chuyển sang chế độ RIÊNG TƯ.",
            conversationId, claimedByStaffName, claimedByStaffId);

        // 1. Thông báo cho toàn bộ hàng đợi support_queue để các Nhân viên/Admin khác xóa ngay cuộc hội thoại khỏi màn hình chờ
        await _hubContext.Clients.Group(ChatHub.SupportQueueGroupName).ReceiveSupportConversationClaimed(
            conversationId,
            claimedByStaffId,
            claimedByStaffName,
            claimedAt);

        // 2. Cập nhật riêng cho khách hàng khởi tạo
        var initiatorGroup = ChatHub.GetUserGroupName(initiatorUserId);
        await _hubContext.Clients.Group(initiatorGroup).ReceiveChatMessage(systemMessage);
        await _hubContext.Clients.Group(initiatorGroup).ReceiveConversationUpdated(updatedForInitiator);

        // 3. Cập nhật riêng cho Nhân viên/Admin đã bấm duyệt
        var staffGroup = ChatHub.GetUserGroupName(claimedByStaffId);
        await _hubContext.Clients.Group(staffGroup).ReceiveChatMessage(systemMessage);
        await _hubContext.Clients.Group(staffGroup).ReceiveConversationUpdated(updatedForClaimedStaff);
    }

    public async Task NotifyMessagesReadAsync(
        Guid conversationId,
        Guid readerUserId,
        Guid? notifyTargetUserId,
        DateTime readAt,
        CancellationToken cancellationToken = default)
    {
        if (notifyTargetUserId.HasValue && notifyTargetUserId.Value != Guid.Empty)
        {
            var targetGroup = ChatHub.GetUserGroupName(notifyTargetUserId.Value);
            await _hubContext.Clients.Group(targetGroup).ReceiveMessagesRead(conversationId, readerUserId, readAt);
        }
    }
}
