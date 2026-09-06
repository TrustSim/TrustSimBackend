using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Identity.Commands;

/// <summary>
/// Command gửi mã OTP xác thực qua tin nhắn SMS tới số điện thoại người dùng.
/// </summary>
public class SendPhoneOtpCommand : IRequest<SendOtpResponseDto>
{
    /// <summary>
    /// Số điện thoại nhận mã OTP (Định dạng VN: 098..., +8498..., 8498...).
    /// </summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// Mục đích xác thực (PhoneVerification, Registration, PasswordReset, FinancialTransaction, SimTransferApproval).
    /// </summary>
    public OtpPurpose Purpose { get; set; } = OtpPurpose.PhoneVerification;
}
