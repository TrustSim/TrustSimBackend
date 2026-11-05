using MediatR;
using Microsoft.AspNetCore.Http;
using TrustSimBackend.Application.Features.Chat.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Chat.DTOs.Requests;

/// <summary>
/// Command gửi tin nhắn mới trong cuộc hội thoại (Direct hoặc Support).
/// </summary>
public record SendChatMessageCommand(
    Guid ConversationId,
    Guid SenderId,
    UserRole SenderRole,
    string? Content,
    IFormFile? AttachmentFile = null) : IRequest<ChatMessageDto>;
