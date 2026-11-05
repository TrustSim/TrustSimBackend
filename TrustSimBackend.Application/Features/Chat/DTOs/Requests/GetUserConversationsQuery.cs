using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Chat.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Chat.DTOs.Requests;

/// <summary>
/// Truy vấn lấy danh sách cuộc hội thoại của người dùng hiện tại (áp dụng quy tắc ẩn tuyệt đối các hội thoại Support đã được Staff/Admin khác duyệt).
/// </summary>
public record GetUserConversationsQuery(
    Guid CurrentUserId,
    UserRole CurrentUserRole,
    ConversationType? Type = null,
    SupportConversationStatus? SupportStatus = null,
    int PageIndex = 1,
    int PageSize = 20) : IRequest<PagedResult<ChatConversationSummaryDto>>;
