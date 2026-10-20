using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Filters;

/// <summary>
/// Bộ lọc truy vấn danh sách hồ sơ thẩm định eKYC dành cho Quản trị viên.
/// </summary>
public class AdminKycQueryFilter : BaseQueryFilter<KycSortBy>
{
    /// <summary>
    /// Trạng thái phê duyệt eKYC (PENDING, APPROVED, REJECTED...).
    /// </summary>
    public VerificationStatus? Status { get; set; }

    /// <summary>
    /// Điểm khớp khuôn mặt tối thiểu (0.0 - 1.0).
    /// </summary>
    public float? MinSimilarityScore { get; set; }
}

/// <summary>
/// Bộ lọc truy vấn danh sách tài khoản thành viên hệ thống dành cho Quản trị viên.
/// </summary>
public class AdminUsersQueryFilter : BaseQueryFilter<UserAccountSortBy>
{
    /// <summary>
    /// Vai trò người dùng (USER, SELLER, ADMIN, CONSULTANT...).
    /// </summary>
    public UserRole? Role { get; set; }

    /// <summary>
    /// Trạng thái kích hoạt tài khoản.
    /// </summary>
    public bool? IsActive { get; set; }

    /// <summary>
    /// Trạng thái thẩm định eKYC của người dùng.
    /// </summary>
    public VerificationStatus? KycStatus { get; set; }

    /// <summary>
    /// Số dư ví tối thiểu (VNĐ).
    /// </summary>
    public decimal? MinWalletBalance { get; set; }

    /// <summary>
    /// Số dư ví tối đa (VNĐ).
    /// </summary>
    public decimal? MaxWalletBalance { get; set; }
}
