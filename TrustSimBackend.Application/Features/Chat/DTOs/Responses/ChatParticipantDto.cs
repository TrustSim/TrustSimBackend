using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Chat.DTOs.Responses;

/// <summary>
/// Thông tin người tham gia trong cuộc hội thoại.
/// </summary>
public record ChatParticipantDto(
    Guid UserId,
    string UserName,
    string FullName,
    string Email,
    string PhoneNumber,
    string AvatarUrl,
    UserRole Role,
    bool IsIdentityVerified);
