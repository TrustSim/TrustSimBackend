namespace TrustSimBackend.Application.Features.Identity.DTOs.Responses;

/// <summary>
/// DTO trả về kết quả khởi tạo đăng ký tài khoản (Bước 1: thông báo gửi OTP qua Email).
/// </summary>
public class RegisterInitiatedDto
{
    public string Email { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int CooldownSeconds { get; set; }
    public int ExpiryMinutes { get; set; }
}
