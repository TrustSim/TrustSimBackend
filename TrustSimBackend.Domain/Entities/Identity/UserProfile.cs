using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Identity;

/// <summary>
/// Thông tin cá nhân người dùng
/// </summary>
public class UserProfile : BaseEntity
{
    public Guid UserProfileId { get => Id; private set => Id = value; }

    // Foreign Key (1-1 với UserAccount)
    public Guid UserAccountId { get; set; }

    public string FullName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string IdentityNumber { get; set; } = string.Empty;
    public string AvatarImg { get; set; } = string.Empty;
    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.PENDING_VERIFICATION;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation Property
    /// <summary>
    /// Tài khoản liên kết với thông tin cá nhân này
    /// </summary>
    public virtual UserAccount? UserAccount { get; set; }

    public virtual ICollection<IdentityVerification> IdentityVerifications { get; set; } = new List<IdentityVerification>();

}
