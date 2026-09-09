using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Responses;

public class AdminUserDetailDto
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string IdentityNumber { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string AvatarImg { get; set; } = string.Empty;
    public VerificationStatus VerificationStatus { get; set; }
    public decimal AvailableBalance { get; set; }
    public decimal LockedBalance { get; set; }
    public bool HasPkiKeys { get; set; }
    public DateTime CreatedDate { get; set; }
}
