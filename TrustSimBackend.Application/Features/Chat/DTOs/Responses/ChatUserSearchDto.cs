using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Chat.DTOs.Responses;

/// <summary>
/// Thông tin người dùng đã xác thực danh tính (eKYC) trả về khi tìm kiếm qua Email hoặc Số điện thoại liên lạc.
/// </summary>
public record ChatUserSearchDto(
    Guid UserId,
    string UserName,
    string FullName,
    string Email,
    string PhoneNumber,
    string AvatarUrl,
    VerificationStatus VerificationStatus);
