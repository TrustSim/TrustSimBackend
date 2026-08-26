using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Identity;

/// <summary>
/// Ghi lại lịch sử mỗi lần người dùng gửi yêu cầu xác minh danh tính KYC (CMND/CCCD/Hộ chiếu).
/// Mỗi lần gửi tài liệu là một bản ghi mới — cho phép theo dõi lịch sử và lý do từ chối.
/// Không được phép xóa — là bằng chứng pháp lý về quá trình định danh người dùng.
/// </summary>
public class IdentityVerification : AuditableEntity
{
    /// <summary>Khóa chính của lần xác minh này (alias cho <see cref="BaseEntity.Id"/>).</summary>
    public Guid IdentityVerificationId { get => Id; private set => Id = value; }

    // --- Foreign Key ---
    /// <summary>ID của hồ sơ cá nhân (<see cref="UserProfile"/>) thực hiện yêu cầu xác minh này.</summary>
    public Guid UserProfileId { get; set; }

    // --- Trạng thái ---
    /// <summary>Trạng thái xử lý của yêu cầu xác minh: PENDING_VERIFICATION, VERIFIED, REJECTED, EXPIRED.</summary>
    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.PENDING_VERIFICATION;

    /// <summary>Thời điểm người dùng nộp hồ sơ xác minh lên hệ thống.</summary>
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Thời điểm Admin duyệt hoặc từ chối hồ sơ. Null nếu chưa được xử lý.</summary>
    public DateTime? VerifiedAt { get; set; }

    /// <summary>Lý do từ chối do Admin điền vào khi trạng thái là REJECTED. Rỗng nếu được duyệt.</summary>
    public string? RejectionReason { get; set; } = string.Empty;

    // --- Navigation Property ---
    /// <summary>Hồ sơ cá nhân đã gửi yêu cầu xác minh này.</summary>
    public virtual UserProfile? UserProfile { get; set; }
}