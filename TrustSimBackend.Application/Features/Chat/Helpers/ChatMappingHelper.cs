using TrustSimBackend.Application.Features.Chat.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Chat;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Chat.Helpers;

/// <summary>
/// Helper ánh xạ thực thể ChatConversation và ChatMessage sang các DTO phản hồi chuẩn.
/// </summary>
public static class ChatMappingHelper
{
    public const int DefaultChatImageRetentionDays = 7;

    public static ChatParticipantDto MapParticipant(UserAccount? user)
    {
        if (user == null)
        {
            return new ChatParticipantDto(
                Guid.Empty,
                "unknown",
                "Người dùng",
                string.Empty,
                string.Empty,
                string.Empty,
                UserRole.USER,
                false);
        }

        var profile = user.UserProfile;
        var fullName = !string.IsNullOrWhiteSpace(profile?.FullName) ? profile.FullName : (user.UserName ?? "Người dùng");
        var email = !string.IsNullOrWhiteSpace(profile?.Email) ? profile.Email : (user.Email ?? string.Empty);
        var phone = profile?.PhoneNumber ?? string.Empty;
        var avatar = profile?.AvatarImg ?? string.Empty;
        var isVerified = profile?.VerificationStatus == VerificationStatus.VERIFIED;

        return new ChatParticipantDto(
            user.Id,
            user.UserName ?? string.Empty,
            fullName,
            email,
            phone,
            avatar,
            user.Role,
            isVerified);
    }

    public static ChatConversationSummaryDto MapConversationSummary(
        ChatConversation conversation,
        Guid viewerUserId,
        UserRole viewerRole)
    {
        var initiatorDto = MapParticipant(conversation.InitiatorUser);
        var participantDto = conversation.ParticipantUser != null ? MapParticipant(conversation.ParticipantUser) : null;
        var assignedStaffDto = conversation.AssignedStaff != null ? MapParticipant(conversation.AssignedStaff) : null;

        bool isInitiator = conversation.InitiatorUserId == viewerUserId;
        int unreadCount = isInitiator ? conversation.UnreadCountInitiator : conversation.UnreadCountTarget;

        string displayTitle;
        string displayAvatar;

        if (conversation.Type == ConversationType.DIRECT)
        {
            var peer = isInitiator ? participantDto : initiatorDto;
            displayTitle = peer?.FullName ?? "Người dùng TrustSim";
            displayAvatar = peer?.AvatarUrl ?? string.Empty;
        }
        else
        {
            if (isInitiator)
            {
                if (conversation.SupportStatus == SupportConversationStatus.RESOLVED)
                {
                    displayTitle = assignedStaffDto != null
                        ? $"Hỗ trợ Sàn TrustSim ({assignedStaffDto.FullName} - Đã kết thúc)"
                        : "Hỗ trợ Sàn TrustSim (Đã kết thúc)";
                }
                else
                {
                    displayTitle = assignedStaffDto != null
                        ? $"Hỗ trợ Sàn TrustSim ({assignedStaffDto.FullName})"
                        : "Hỗ trợ Sàn TrustSim (Đang chờ tiếp nhận)";
                }
                displayAvatar = assignedStaffDto?.AvatarUrl ?? string.Empty;
            }
            else
            {
                displayTitle = conversation.SupportStatus == SupportConversationStatus.RESOLVED
                    ? $"[Hỗ trợ - Đã đóng] {initiatorDto.FullName}"
                    : $"[Hỗ trợ] {initiatorDto.FullName}";
                displayAvatar = initiatorDto.AvatarUrl;
            }
        }

        var isStaffOrAdmin = viewerRole == UserRole.ADMIN || viewerRole == UserRole.CONSULTANT;
        var canClaim = conversation.Type == ConversationType.SUPPORT
                    && conversation.SupportStatus == SupportConversationStatus.PENDING
                    && conversation.AssignedStaffId == null
                    && isStaffOrAdmin
                    && !isInitiator;

        var isPrivateSupport = conversation.Type == ConversationType.SUPPORT
                            && (conversation.SupportStatus == SupportConversationStatus.CLAIMED
                                || conversation.SupportStatus == SupportConversationStatus.RESOLVED)
                            && conversation.AssignedStaffId.HasValue;

        return new ChatConversationSummaryDto(
            Id: conversation.Id,
            Type: conversation.Type,
            SupportStatus: conversation.SupportStatus,
            DisplayTitle: displayTitle,
            DisplayAvatarUrl: displayAvatar,
            Initiator: initiatorDto,
            Participant: participantDto,
            AssignedStaff: assignedStaffDto,
            ClaimedAt: conversation.ClaimedAt,
            LastMessagePreview: conversation.LastMessagePreview,
            LastMessageAt: conversation.LastMessageAt ?? conversation.CreatedDate,
            LastMessageSenderId: conversation.LastMessageSenderId,
            UnreadCount: unreadCount,
            IsPrivateSupport: isPrivateSupport,
            CanClaim: canClaim,
            CreatedDate: conversation.CreatedDate);
    }

    public static ChatMessageDto MapMessage(ChatMessage message)
        => MapMessageWithRetention(message, DefaultChatImageRetentionDays);

    public static ChatMessageDto MapMessageWithRetention(ChatMessage message, int chatImageRetentionDays)
    {
        var sender = message.Sender;
        var profile = sender?.UserProfile;
        var senderName = message.MessageType == ChatMessageType.SYSTEM
            ? "Hệ thống TrustSim"
            : (!string.IsNullOrWhiteSpace(profile?.FullName) ? profile.FullName : (sender?.UserName ?? "Người dùng"));
        var senderAvatar = profile?.AvatarImg ?? string.Empty;
        var senderRole = sender?.Role ?? UserRole.USER;

        DateTime? attachmentExpiresAt = null;
        bool isAttachmentExpired = false;

        if (message.MessageType == ChatMessageType.IMAGE)
        {
            var effectiveDays = chatImageRetentionDays > 0 ? chatImageRetentionDays : DefaultChatImageRetentionDays;
            var createdUtc = message.CreatedDate == default ? DateTime.UtcNow : message.CreatedDate;
            attachmentExpiresAt = createdUtc.AddDays(effectiveDays);
            isAttachmentExpired = DateTime.UtcNow >= attachmentExpiresAt.Value || string.IsNullOrWhiteSpace(message.AttachmentUrl);
        }

        return new ChatMessageDto(
            Id: message.Id,
            ConversationId: message.ConversationId,
            SenderId: message.SenderId,
            SenderName: senderName,
            SenderAvatarUrl: senderAvatar,
            SenderRole: senderRole,
            MessageType: message.MessageType,
            Content: message.Content,
            AttachmentUrl: isAttachmentExpired ? null : message.AttachmentUrl,
            AttachmentFileName: message.AttachmentFileName,
            IsRead: message.IsRead,
            ReadAt: message.ReadAt,
            CreatedDate: message.CreatedDate,
            AttachmentExpiresAt: attachmentExpiresAt,
            IsAttachmentExpired: isAttachmentExpired);
    }
}
