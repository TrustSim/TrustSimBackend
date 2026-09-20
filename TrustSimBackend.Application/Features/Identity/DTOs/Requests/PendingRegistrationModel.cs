namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

/// <summary>
/// Model lưu trữ thông tin đăng ký tài khoản tạm thời trong bộ nhớ đệm (Cache) trong lúc chờ xác thực Email OTP.
/// </summary>
public class PendingRegistrationModel
{
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
