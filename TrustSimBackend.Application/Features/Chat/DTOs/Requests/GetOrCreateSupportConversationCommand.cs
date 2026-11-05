using MediatR;
using TrustSimBackend.Application.Features.Chat.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Chat.DTOs.Requests;

/// <summary>
/// Command mở mới hoặc lấy lại cuộc hội thoại hỗ trợ với Sàn TrustSim (SUPPORT).
/// </summary>
public record GetOrCreateSupportConversationCommand(
    Guid CurrentUserId,
    UserRole CurrentUserRole,
    string? InitialMessage = null) : IRequest<ChatConversationSummaryDto>;
