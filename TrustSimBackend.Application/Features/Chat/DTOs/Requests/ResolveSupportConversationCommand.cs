using MediatR;
using TrustSimBackend.Application.Features.Chat.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Chat.DTOs.Requests;

/// <summary>
/// Command đóng / kết thúc một phiên hội thoại hỗ trợ với Sàn (chuyển trạng thái sang RESOLVED).
/// </summary>
public record ResolveSupportConversationCommand(
    Guid ConversationId,
    Guid CurrentUserId,
    UserRole CurrentUserRole) : IRequest<ChatConversationSummaryDto>;
