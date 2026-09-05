namespace TrustSimBackend.Application.Features.Identity.DTOs;

/// <summary>
/// DTO chứa Refresh Token khi gửi yêu cầu làm mới hoặc đăng xuất qua Request Body (tùy chọn).
/// </summary>
public class RefreshTokenRequestDto
{
    /// <summary>
    /// Refresh Token gửi từ Request Body (nếu client là Mobile App hoặc Postman không dùng Cookie).
    /// Nếu để trống, Server sẽ tự động lấy từ HttpOnly Cookie "refreshToken".
    /// </summary>
    public string? RefreshToken { get; set; }
}
