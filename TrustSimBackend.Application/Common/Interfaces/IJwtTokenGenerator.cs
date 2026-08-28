using TrustSimBackend.Domain.Entities.Identity;

namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Dịch vụ tạo JWT Bearer Token phục vụ xác thực và phân quyền người dùng.
/// </summary>
public interface IJwtTokenGenerator
{
    /// <summary>
    /// Tạo chuỗi JWT Token chứa các Claims (UserId, UserName, Email, Role, FullName).
    /// </summary>
    /// <param name="user">Tài khoản người dùng</param>
    /// <param name="profile">Hồ sơ cá nhân (nếu có)</param>
    /// <returns>Chuỗi JWT Token và thời hạn hết hạn</returns>
    (string Token, DateTime ExpiresAt) GenerateToken(UserAccount user, UserProfile? profile);
}
