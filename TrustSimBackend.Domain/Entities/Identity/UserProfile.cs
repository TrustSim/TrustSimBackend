using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Identity;

/// <summary>
/// Lưu trữ thông tin cá nhân (KYC) của người dùng, tách biệt khỏi thông tin đăng nhập.
/// Liên kết 1-1 với <see cref="UserAccount"/>.
/// Có thể bị xóa mềm nếu Admin thu hồi hồ sơ, nhưng lịch sử xác minh vẫn được giữ trong <see cref="IdentityVerification"/>.
/// </summary>
public class UserProfile : FullAuditableEntity
{
    /// <summary>Khóa chính của hồ sơ cá nhân (alias cho <see cref="BaseEntity.Id"/>).</summary>
    public Guid UserProfileId { get => Id; private set => Id = value; }

    // --- Foreign Key ---
    /// <summary>ID của tài khoản đăng nhập liên kết 1-1 với hồ sơ này.</summary>
    public Guid UserAccountId { get; set; }

    // --- Thông tin cá nhân ---
    /// <summary>Họ và tên đầy đủ của người dùng (theo giấy tờ tùy thân).</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Ngày tháng năm sinh của người dùng.</summary>
    public DateTime DateOfBirth { get; set; }

    /// <summary>Địa chỉ email liên lạc của người dùng.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Số điện thoại liên lạc (không nhất thiết là SIM đấu giá).</summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>Địa chỉ thường trú hoặc địa chỉ liên hệ của người dùng.</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>Số CMND / CCCD / Hộ chiếu của người dùng (dùng để xác minh danh tính KYC).</summary>
    public string IdentityNumber { get; set; } = string.Empty;

    /// <summary>URL ảnh đại diện của người dùng (lưu trên cloud storage).</summary>
    public string AvatarImg { get; set; } = string.Empty;

    /// <summary>Trạng thái xác minh danh tính KYC hiện tại: PENDING_VERIFICATION, VERIFIED, REJECTED, EXPIRED.</summary>
    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.PENDING_VERIFICATION;

    // --- Navigation Properties ---
    /// <summary>Tài khoản đăng nhập liên kết với hồ sơ cá nhân này (quan hệ 1-1).</summary>
    public virtual UserAccount? UserAccount { get; set; }

    /// <summary>Danh sách lịch sử các lần gửi yêu cầu xác minh danh tính (quan hệ 1-N).</summary>
    public virtual ICollection<IdentityVerification> IdentityVerifications { get; set; } = new List<IdentityVerification>();
}
