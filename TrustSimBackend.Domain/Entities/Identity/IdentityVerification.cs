namespace TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Enums;

/// <summary>
/// Xác minh danh tính
/// </summary>
public class IdentityVerification : BaseEntity
{
    public Guid IdentityVerificationId { get => Id; private set => Id = value; }

    //Foreign Key
    public Guid UserProfileId { get; set; }

    //Navigation Property
    public virtual UserProfile? UserProfile { get; set; }

    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.PENDING_VERIFICATION;

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? VerifiedAt { get; set; }
    public string RejectionReason { get; set; } = string.Empty;

}