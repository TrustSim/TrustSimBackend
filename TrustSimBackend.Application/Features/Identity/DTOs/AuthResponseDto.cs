namespace TrustSimBackend.Application.Features.Identity.DTOs;

/// <summary>
/// DTO trả về cho Client sau khi đăng ký hoặc đăng nhập thành công.
/// </summary>
public class AuthResponseDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
