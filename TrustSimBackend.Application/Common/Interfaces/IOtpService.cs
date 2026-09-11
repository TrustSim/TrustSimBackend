using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Interface cung cấp dịch vụ sinh mã OTP, băm bảo mật, kiểm tra giới hạn (Rate Limit / Cooldown / Max Attempts) và xác thực mã OTP.
/// </summary>
public interface IOtpService
{
    /// <summary>
    /// Sinh mã OTP ngẫu nhiên, băm lưu Cache an toàn và gửi SMS qua SpeedSMS.
    /// </summary>
    Task<SendOtpResponseDto> GenerateAndSendPhoneOtpAsync(string phoneNumber, OtpPurpose purpose, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra xác thực mã OTP do người dùng nhập vào.
    /// </summary>
    Task<VerifyOtpResponseDto> VerifyPhoneOtpAsync(string phoneNumber, string otpCode, OtpPurpose purpose, bool consumeOnSuccess = true);
}
