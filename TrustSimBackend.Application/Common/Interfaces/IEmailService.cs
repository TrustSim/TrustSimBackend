using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Interface cung cấp dịch vụ gửi Email thông báo và mã OTP.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Gửi email đơn giản dạng HTML tới một địa chỉ nhận.
    /// </summary>
    Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gửi email với cấu trúc đầy đủ (nhiều người nhận, CC, BCC, đính kèm file).
    /// </summary>
    Task<bool> SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gửi email chứa mã OTP xác thực (Quên mật khẩu, Đăng ký tài khoản, Xác thực giao dịch).
    /// </summary>
    Task<bool> SendOtpEmailAsync(string toEmail, string otpCode, string purpose = "xác thực tài khoản", int expiryMinutes = 5, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gửi email thông báo biến động tài chính hoặc kết quả đấu giá SIM.
    /// </summary>
    Task<bool> SendNotificationEmailAsync(string toEmail, string title, string message, string? actionUrl = null, string? actionText = null, CancellationToken cancellationToken = default);
}
