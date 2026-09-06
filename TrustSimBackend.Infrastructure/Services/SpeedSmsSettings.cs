namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Cấu hình kết nối tới dịch vụ SpeedSMS API Gateway.
/// </summary>
public class SpeedSmsSettings
{
    public const string SectionName = "SpeedSms";

    /// <summary>URL gốc của API SpeedSMS.</summary>
    public string RootUrl { get; set; } = "https://api.speedsms.vn/index.php";

    /// <summary>API Access Token do SpeedSMS cấp.</summary>
    public string ApiKey { get; set; } = "BZjPOTJHM-XX-A4NB5okKWUDEF-8yZYf";

    /// <summary>Tên Sender / Brandname (nếu dùng Brandname).</summary>
    public string Sender { get; set; } = "";

    /// <summary>
    /// Loại hình gửi SMS (1: QC, 2: CSKH, 3: Brandname, 4: Brandname Notify, 5: Gateway SIM cá nhân qua App Android).
    /// Mặc định 5 (Gateway) theo phương án thiết bị cá nhân của bạn.
    /// </summary>
    public int SmsType { get; set; } = 5;

    /// <summary>Thời gian timeout của HTTP Request (giây).</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Thời hạn hiệu lực của mã OTP (phút).</summary>
    public int OtpExpiryMinutes { get; set; } = 5;

    /// <summary>Thời gian chờ tối thiểu giữa 2 lần gửi lại OTP (giây).</summary>
    public int CooldownSeconds { get; set; } = 60;

    /// <summary>Số lần nhập sai tối đa trước khi hủy mã OTP.</summary>
    public int MaxFailedAttempts { get; set; } = 3;

    /// <summary>Khóa bí mật dùng để băm mã OTP trong bộ nhớ đệm (HMAC/SHA256).</summary>
    public string OtpHashingSecret { get; set; } = "TrustSimSecretForOtpSalt2026";
}
