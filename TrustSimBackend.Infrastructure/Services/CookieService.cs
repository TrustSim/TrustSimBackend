using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using TrustSimBackend.Application.Common.Interfaces;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Triển khai ICookieService quản lý trích xuất và đính kèm HttpOnly Cookies cho Access Token và Refresh Token.
/// </summary>
public class CookieService : ICookieService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHostEnvironment _environment;

    public CookieService(IHttpContextAccessor httpContextAccessor, IHostEnvironment environment)
    {
        _httpContextAccessor = httpContextAccessor;
        _environment = environment;
    }

    public void SetAuthCookies(string accessToken, DateTime accessTokenExpiresAt, string refreshToken, DateTime refreshTokenExpiresAt)
    {
        var response = _httpContextAccessor.HttpContext?.Response;
        if (response == null) return;

        var isProduction = _environment.IsProduction();

        // 1. Cấu hình Cookie cho Access Token
        var accessCookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = isProduction, // Yêu cầu HTTPS trên môi trường Production
            SameSite = isProduction ? SameSiteMode.Strict : SameSiteMode.Lax,
            Expires = new DateTimeOffset(accessTokenExpiresAt),
            Path = "/"
        };
        response.Cookies.Append("accessToken", accessToken, accessCookieOptions);

        // 2. Cấu hình Cookie cho Refresh Token
        var refreshCookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = isProduction,
            SameSite = isProduction ? SameSiteMode.Strict : SameSiteMode.Lax,
            Expires = new DateTimeOffset(refreshTokenExpiresAt),
            Path = "/api/auth" // Giới hạn phạm vi chỉ truyền khi gọi các API thuộc /api/auth
        };
        response.Cookies.Append("refreshToken", refreshToken, refreshCookieOptions);
    }

    public void ClearAuthCookies()
    {
        var response = _httpContextAccessor.HttpContext?.Response;
        if (response == null) return;

        var isProduction = _environment.IsProduction();

        response.Cookies.Delete("accessToken", new CookieOptions 
        { 
            Path = "/", 
            HttpOnly = true, 
            Secure = isProduction,
            SameSite = isProduction ? SameSiteMode.Strict : SameSiteMode.Lax
        });

        response.Cookies.Delete("refreshToken", new CookieOptions 
        { 
            Path = "/api/auth", 
            HttpOnly = true, 
            Secure = isProduction,
            SameSite = isProduction ? SameSiteMode.Strict : SameSiteMode.Lax
        });
    }

    public string? GetRefreshToken()
    {
        return _httpContextAccessor.HttpContext?.Request.Cookies["refreshToken"];
    }

    public string? GetAccessToken()
    {
        return _httpContextAccessor.HttpContext?.Request.Cookies["accessToken"];
    }
}
