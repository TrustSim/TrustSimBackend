using MediatR;
using TrustSimBackend.Application.Features.Chat.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Chat.DTOs.Requests;

/// <summary>
/// Command mở mới hoặc lấy lại cuộc hội thoại 1-1 (DIRECT) giữa 2 người dùng đã xác thực danh tính (eKYC).
/// </summary>
public record GetOrCreateDirectConversationCommand(
    Guid CurrentUserId,
    UserRole CurrentUserRole,
    Guid TargetUserId,
    string? InitialMessage = null) : IRequest<ChatConversationSummaryDto>;
