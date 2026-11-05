using MediatR;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Chat.DTOs.Requests;

/// <summary>
/// Command đánh dấu đã đọc toàn bộ tin nhắn trong cuộc hội thoại.
/// </summary>
public record MarkConversationReadCommand(
    Guid ConversationId,
    Guid ReaderUserId,
    UserRole ReaderRole) : IRequest<int>;
