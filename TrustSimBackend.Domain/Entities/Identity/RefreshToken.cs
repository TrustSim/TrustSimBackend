using TrustSimBackend.Domain.Base;

namespace TrustSimBackend.Domain.Entities.Identity;

/// <summary>
/// Đại diện cho Refresh Token lưu trữ trong cơ sở dữ liệu phục vụ duy trì phiên đăng nhập và xoay vòng token.
/// </summary>
public class RefreshToken : BaseEntity
{
    /// <summary>Khóa ngoại liên kết với tài khoản người dùng sở hữu token.</summary>
    public Guid UserAccountId { get; set; }

    /// <summary>Chuỗi token ngẫu nhiên bảo mật cao (Opaque Token).</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Mã định danh JTI của Access Token được cấp cùng thời điểm.</summary>
    public string JwtId { get; set; } = string.Empty;

    /// <summary>Thời điểm token hết hạn (UTC).</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Địa chỉ IP tạo ra token.</summary>
    public string? CreatedByIp { get; set; }

    /// <summary>Đã được sử dụng để đổi lấy Token mới chưa (phục vụ cơ chế Token Rotation).</summary>
    public bool IsUsed { get; set; } = false;

    /// <summary>Đã bị thu hồi / hủy bỏ hiệu lực thủ công chưa.</summary>
    public bool IsRevoked { get; set; } = false;

    /// <summary>Thời điểm token bị thu hồi (nếu có).</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>Địa chỉ IP thực hiện thu hồi token.</summary>
    public string? RevokedByIp { get; set; }

    /// <summary>Token mới thay thế cho token này (khi xoay vòng token thành công).</summary>
    public string? ReplacedByToken { get; set; }

    /// <summary>Lý do thu hồi token (Logout, Token Compromised, Session Timeout...).</summary>
    public string? ReasonRevoked { get; set; }

    // --- Computed Properties ---
    /// <summary>Kiểm tra token đã hết hạn chưa.</summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    /// <summary>Kiểm tra token có đang hợp lệ để sử dụng không.</summary>
    public bool IsTokenValid => !IsRevoked && !IsUsed && !IsExpired;

    // --- Navigation Properties ---
    /// <summary>Tài khoản người dùng sở hữu token này.</summary>
    public virtual UserAccount? UserAccount { get; set; }
}
