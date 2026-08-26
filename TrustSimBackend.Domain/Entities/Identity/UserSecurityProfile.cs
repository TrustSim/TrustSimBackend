using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Identity;

/// <summary>
/// Lưu trữ cặp khóa ký số (PKI Key Pair) của người dùng, áp dụng nguyên lý tách bạch dữ liệu nhạy cảm.
/// Public Key dùng để xác minh chữ ký. Private Key được mã hóa AES-256 phía client trước khi lưu lên server.
/// Không bao giờ bị xóa — lịch sử khóa cần được lưu vĩnh viễn để kiểm toán và xác minh chữ ký cũ.
/// </summary>
public class UserSecurityProfile : AuditableEntity
{
    /// <summary>Khóa chính của hồ sơ bảo mật (alias cho <see cref="BaseEntity.Id"/>).</summary>
    public Guid UserSecurityProfileId { get => Id; private set => Id = value; }

    /// <summary>
    /// Khóa công khai (Public Key) dùng để xác minh chữ ký số của người dùng.
    /// Được lưu dạng Base64 encoded PEM string.
    /// </summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>
    /// Khóa bí mật (Private Key) đã được mã hóa AES-256 bằng mã PIN của người dùng.
    /// Server không bao giờ biết được Private Key gốc — chỉ client mới có thể giải mã.
    /// </summary>
    public string EncryptedPrivateKey { get; set; } = string.Empty;

    /// <summary>
    /// Hash của mã PIN 6 số (dùng Bcrypt/Argon2) để xác minh người dùng khi ký tài liệu.
    /// Không lưu PIN gốc.
    /// </summary>
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>Thời điểm cặp khóa này được tạo ra. Dùng để kiểm tra thời hạn hợp lệ.</summary>
    public DateTime KeyGeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Trạng thái của cặp khóa: ACTIVE (đang sử dụng) hoặc REVOKED (đã thu hồi).</summary>
    public UserSecurityProfileStatus UserSecurityProfileStatus { get; set; } = UserSecurityProfileStatus.ACTIVE;

    // --- Foreign Key ---
    /// <summary>ID của tài khoản sở hữu cặp khóa này (quan hệ 1-1 với UserAccount).</summary>
    public Guid UserAccountId { get; set; }

    // --- Navigation Property ---
    /// <summary>Tài khoản đăng nhập sở hữu cặp khóa ký số này.</summary>
    public virtual UserAccount? UserAccount { get; set; }
}