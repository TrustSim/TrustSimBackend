using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Chat.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Chat.DTOs.Requests;

/// <summary>
/// Truy vấn lấy danh sách tin nhắn trong một cuộc hội thoại (chặn 403 nếu không có quyền truy cập).
/// </summary>
public record GetConversationMessagesQuery(
    Guid ConversationId,
    Guid CurrentUserId,
    UserRole CurrentUserRole,
    int PageIndex = 1,
    int PageSize = 50) : IRequest<PagedResult<ChatMessageDto>>;
