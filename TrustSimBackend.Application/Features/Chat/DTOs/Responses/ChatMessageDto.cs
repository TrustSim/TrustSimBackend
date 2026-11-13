using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Chat.DTOs.Responses;

/// <summary>
/// Thông tin chi tiết của một tin nhắn trong cuộc hội thoại.
/// </summary>
public record ChatMessageDto(
    Guid Id,
    Guid ConversationId,
    Guid SenderId,
    string SenderName,
    string SenderAvatarUrl,
    UserRole SenderRole,
    ChatMessageType MessageType,
    string Content,
    string? AttachmentUrl,
    string? AttachmentFileName,
    bool IsRead,
    DateTime? ReadAt,
    DateTime CreatedDate,
    DateTime? AttachmentExpiresAt = null,
    bool IsAttachmentExpired = false);
