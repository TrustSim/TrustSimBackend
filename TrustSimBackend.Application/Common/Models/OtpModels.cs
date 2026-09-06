using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Common.Models;

/// <summary>
/// DTO phản hồi kết quả sau khi gửi yêu cầu sinh mã OTP SMS.
/// </summary>
public class SendOtpResponseDto
{
    /// <summary>Cho biết gửi OTP thành công hay thất bại.</summary>
    public bool IsSuccess { get; set; }

    /// <summary>Thông điệp kết quả.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Thời gian chờ tối thiểu trước khi được gửi lại OTP tiếp theo (giây).</summary>
    public int CooldownSeconds { get; set; }

    /// <summary>Thời hạn hiệu lực của mã OTP (phút).</summary>
    public int ExpiryMinutes { get; set; } = 5;
}

/// <summary>
/// DTO phản hồi kết quả sau khi kiểm tra mã OTP.
/// </summary>
public class VerifyOtpResponseDto
{
    /// <summary>Mã OTP có hợp lệ và còn hạn hay không.</summary>
    public bool IsValid { get; set; }

    /// <summary>Thông điệp chi tiết.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Số điện thoại đã xác thực thành công.</summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>Mục đích xác thực.</summary>
    public OtpPurpose Purpose { get; set; }
}

/// <summary>
/// Cấu trúc dữ liệu lưu trữ OTP an toàn trong Cache.
/// </summary>
public class OtpCacheEntry
{
    /// <summary>Chuỗi mã OTP đã được băm an toàn (SHA256).</summary>
    public string HashedOtp { get; set; } = string.Empty;

    /// <summary>Số lần người dùng đã nhập sai mã OTP này.</summary>
    public int FailedAttempts { get; set; }

    /// <summary>Thời điểm mã hết hạn.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Thời điểm kết thúc cooldown để được gửi lại OTP.</summary>
    public DateTimeOffset CooldownUntil { get; set; }

    /// <summary>Trạng thái mã đã được xác thực sử dụng hay chưa.</summary>
    public bool IsConsumed { get; set; }
}
