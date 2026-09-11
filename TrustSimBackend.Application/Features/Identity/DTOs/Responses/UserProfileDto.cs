using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Responses;

/// <summary>
/// Data Transfer Object hiển thị thông tin hồ sơ người dùng chi tiết.
/// </summary>
public class UserProfileDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string AvatarImg { get; set; } = string.Empty;
    public string IdentityNumber { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public VerificationStatus VerificationStatus { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public decimal WalletBalance { get; set; }
}
