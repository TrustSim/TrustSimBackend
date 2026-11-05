using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Chat.DTOs.Responses;

/// <summary>
/// Thông tin tóm tắt của một cuộc hội thoại hiển thị trong danh sách (Direct hoặc Support).
/// </summary>
public record ChatConversationSummaryDto(
    Guid Id,
    ConversationType Type,
    SupportConversationStatus SupportStatus,
    string DisplayTitle,
    string DisplayAvatarUrl,
    ChatParticipantDto Initiator,
    ChatParticipantDto? Participant,
    ChatParticipantDto? AssignedStaff,
    DateTime? ClaimedAt,
    string? LastMessagePreview,
    DateTime? LastMessageAt,
    Guid? LastMessageSenderId,
    int UnreadCount,
    bool IsPrivateSupport,
    bool CanClaim,
    DateTime CreatedDate);
