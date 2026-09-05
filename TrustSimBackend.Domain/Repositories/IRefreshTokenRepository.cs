using TrustSimBackend.Domain.Entities.Identity;

namespace TrustSimBackend.Domain.Repositories;

/// <summary>
/// Repository Interface quản lý truy xuất, lưu trữ và thu hồi thực thể RefreshToken.
/// </summary>
public interface IRefreshTokenRepository
{
    /// <summary>
    /// Tìm kiếm Refresh Token theo chuỗi token (kèm theo thông tin UserAccount).
    /// </summary>
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Thêm mới một Refresh Token vào cơ sở dữ liệu.
    /// </summary>
    Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cập nhật thông tin Refresh Token (đánh dấu đã sử dụng, thay thế...).
    /// </summary>
    void Update(RefreshToken refreshToken);

    /// <summary>
    /// Thu hồi tất cả các Refresh Token đang hoạt động của một tài khoản người dùng (dùng khi phát hiện gian lận reuse).
    /// </summary>
    Task RevokeAllUserTokensAsync(Guid userAccountId, string reason, string? revokedByIp = null, CancellationToken cancellationToken = default);
}
