using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Interface cung cấp dịch vụ gửi tin nhắn SMS / MMS và tra cứu tài khoản qua SpeedSMS Gateway.
/// </summary>
public interface ISpeedSmsService
{
    /// <summary>
    /// Tra cứu thông tin tài khoản SpeedSMS và số dư hiện tại (VND).
    /// </summary>
    Task<SpeedSmsUserInfoResponse?> GetUserInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gửi tin nhắn SMS tới một số điện thoại.
    /// </summary>
    Task<SpeedSmsSendResponse> SendSmsAsync(string phoneNumber, string content, SpeedSmsType? type = null, string? sender = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gửi tin nhắn SMS hàng loạt tới danh sách số điện thoại.
    /// </summary>
    Task<SpeedSmsSendResponse> SendSmsBatchAsync(IEnumerable<string> phoneNumbers, string content, SpeedSmsType? type = null, string? sender = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gửi mã OTP xác thực qua SMS tới số điện thoại.
    /// </summary>
    Task<SpeedSmsSendResponse> SendOtpSmsAsync(string phoneNumber, string otpCode, CancellationToken cancellationToken = default);
}
