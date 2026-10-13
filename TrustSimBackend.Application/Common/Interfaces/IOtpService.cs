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
    /// Kiểm tra xác thực mã OTP do người dùng nhập vào (qua SMS).
    /// </summary>
    Task<VerifyOtpResponseDto> VerifyPhoneOtpAsync(string phoneNumber, string otpCode, OtpPurpose purpose, bool consumeOnSuccess = true);

    /// <summary>
    /// Sinh mã OTP ngẫu nhiên, băm lưu Cache an toàn và gửi qua Email (MailKit).
    /// </summary>
    Task<SendOtpResponseDto> GenerateAndSendEmailOtpAsync(string email, string purpose, int expiryMinutes = 5, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra xác thực mã OTP do người dùng nhập vào (qua Email).
    /// </summary>
    Task<VerifyOtpResponseDto> VerifyEmailOtpAsync(string email, string otpCode, string purpose, bool consumeOnSuccess = true);

    /// <summary>
    /// Chủ động hủy/tiêu thụ mã OTP sau khi giao dịch hoặc lưu trữ CSDL thành công.
    /// </summary>
    Task ConsumeEmailOtpAsync(string email, string purpose);
}
