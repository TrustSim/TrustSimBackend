using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using TrustSimBackend.Application.Common.Interfaces;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Triển khai ICurrentUserService lấy thông tin người dùng được trích xuất và xác thực từ UserContextMiddleware.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return null;

            // 1. Ưu tiên đọc từ HttpContext.Items (đã được UserContextMiddleware trích xuất)
            if (httpContext.Items.TryGetValue("UserId", out var item) && item is Guid guid)
            {
                return guid;
            }

            // 2. Fallback sang ClaimsPrincipal
            var userIdClaim = httpContext.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var id) ? id : null;
        }
    }

    public string? UserName =>
        _httpContextAccessor.HttpContext?.Items["UserName"] as string ??
        _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value;

    public string? Email =>
        _httpContextAccessor.HttpContext?.Items["UserEmail"] as string ??
        _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Email)?.Value;

    public string? Role =>
        _httpContextAccessor.HttpContext?.Items["UserRole"] as string ??
        _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value;

    public bool IsAuthenticated =>
        UserId.HasValue || (_httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false);
}
