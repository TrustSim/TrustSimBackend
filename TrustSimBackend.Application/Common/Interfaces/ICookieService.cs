namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Dịch vụ quản lý thiết lập và trích xuất HttpOnly Cookies phục vụ xác thực người dùng.
/// </summary>
public interface ICookieService
{
    /// <summary>
    /// Ghi nhận 2 HttpOnly Cookie (accessToken và refreshToken) vào Response gửi về Client.
    /// </summary>
    void SetAuthCookies(string accessToken, DateTime accessTokenExpiresAt, string refreshToken, DateTime refreshTokenExpiresAt);

    /// <summary>
    /// Xóa bỏ 2 HttpOnly Cookie (accessToken và refreshToken) khi đăng xuất hoặc thu hồi phiên.
    /// </summary>
    void ClearAuthCookies();

    /// <summary>
    /// Lấy giá trị Refresh Token từ Request Cookie.
    /// </summary>
    string? GetRefreshToken();

    /// <summary>
    /// Lấy giá trị Access Token từ Request Cookie.
    /// </summary>
    string? GetAccessToken();
}
