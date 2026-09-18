using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

/// <summary>
/// Command xác thực mã OTP người dùng nhập vào.
/// </summary>
public class VerifyPhoneOtpCommand : IRequest<VerifyOtpResponseDto>
{
    /// <summary>
    /// Số điện thoại đã nhận mã OTP.
    /// </summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// Mã OTP 6 chữ số người dùng nhập.
    /// </summary>
    public string OtpCode { get; set; } = string.Empty;

    /// <summary>
    /// Mục đích xác thực (Phải khớp với lúc gửi).
    /// </summary>
    public OtpPurpose Purpose { get; set; } = OtpPurpose.PhoneVerification;
}
