using TrustSimBackend.Domain.Entities.Identity;

namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Kết quả sinh chuỗi Access Token (JWT) và Refresh Token.
/// </summary>
public record TokenGenerationResult(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    string JwtId
);

/// <summary>
/// Dịch vụ tạo JWT Access Token và Refresh Token phục vụ xác thực và phân quyền người dùng.
/// </summary>
public interface IJwtTokenGenerator
{
    /// <summary>
    /// Sinh chuỗi JWT Access Token (ngắn hạn) và chuỗi Refresh Token ngẫu nhiên (dài hạn).
    /// </summary>
    /// <param name="user">Tài khoản người dùng</param>
    /// <param name="profile">Hồ sơ cá nhân (nếu có)</param>
    /// <param name="ipAddress">Địa chỉ IP của client (nếu có)</param>
    /// <returns>Đối tượng <see cref="TokenGenerationResult"/> chứa AccessToken, RefreshToken và thời hạn.</returns>
    TokenGenerationResult GenerateTokens(UserAccount user, UserProfile? profile, string? ipAddress = null);
}
