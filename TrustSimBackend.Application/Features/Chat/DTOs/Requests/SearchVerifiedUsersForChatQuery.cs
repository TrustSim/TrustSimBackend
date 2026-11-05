using MediatR;
using TrustSimBackend.Application.Features.Chat.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Chat.DTOs.Requests;

/// <summary>
/// Truy vấn tìm kiếm người dùng đã xác thực danh tính (eKYC) qua Email hoặc Số điện thoại liên lạc trong UserProfile.
/// </summary>
public record SearchVerifiedUsersForChatQuery(
    string Keyword,
    Guid CurrentUserId,
    UserRole CurrentUserRole,
    int Limit = 10) : IRequest<IReadOnlyList<ChatUserSearchDto>>;
