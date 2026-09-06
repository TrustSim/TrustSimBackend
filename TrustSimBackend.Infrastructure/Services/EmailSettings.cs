namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Cấu hình thông tin SMTP kết nối máy chủ gửi email.
/// </summary>
public class EmailSettings
{
    public const string SectionName = "EmailSettings";

    /// <summary>Địa chỉ máy chủ SMTP (VD: smtp.gmail.com, smtp-relay.brevo.com).</summary>
    public string SmtpServer { get; set; } = "smtp.gmail.com";

    /// <summary>Cổng kết nối SMTP (Mặc định 587 cho STARTTLS hoặc 465 cho SSL).</summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>Tên hiển thị người gửi.</summary>
    public string SenderName { get; set; } = "TrustSim Notification System";

    /// <summary>Địa chỉ email người gửi.</summary>
    public string SenderEmail { get; set; } = "no-reply@trustsim.vn";

    /// <summary>Tên đăng nhập tài khoản SMTP.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Mật khẩu ứng dụng (App Password) hoặc mật khẩu SMTP.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Bật SSL khi kết nối.</summary>
    public bool EnableSsl { get; set; } = true;

    /// <summary>Bật STARTTLS chuyển đổi kết nối an toàn.</summary>
    public bool UseStartTls { get; set; } = true;

    /// <summary>Thời gian chờ tối đa khi gửi email (giây).</summary>
    public int TimeoutSeconds { get; set; } = 15;
}
