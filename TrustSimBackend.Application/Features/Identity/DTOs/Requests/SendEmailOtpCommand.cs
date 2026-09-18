using MediatR;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

/// <summary>
/// Command gửi mã OTP 6 chữ số tới địa chỉ Email để xác thực trước khi đăng ký.
/// </summary>
public class SendEmailOtpCommand : IRequest<SendOtpResponseDto>
{
    /// <summary>Địa chỉ email nhận mã OTP.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Mục đích xác thực (mặc định: "đăng ký tài khoản").</summary>
    public string Purpose { get; set; } = "đăng ký tài khoản";

    /// <summary>Thời gian hiệu lực OTP (phút, mặc định 5).</summary>
    public int ExpiryMinutes { get; set; } = 5;
}
