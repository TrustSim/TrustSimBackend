namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Service Interface lấy thông tin định danh của người dùng đang đăng nhập trong HttpContext.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? UserName { get; }
    string? Email { get; }
    string? Role { get; }
    bool IsAuthenticated { get; }
}
