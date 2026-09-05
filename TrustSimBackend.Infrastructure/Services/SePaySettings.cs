namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Cấu hình cổng thanh toán SePay lấy từ appsettings.json (Section: SePay).
/// </summary>
public class SePaySettings
{
    public const string SectionName = "SePay";

    /// <summary>URL Endpoint API của SePay (nếu gọi kiểm tra)</summary>
    public string ApiUrl { get; set; } = "https://my.sepay.vn/userapi";

    /// <summary>API Key trên tài khoản SePay của sàn</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Số tài khoản ngân hàng nhận tiền của sàn</summary>
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>Tên mã ngân hàng (VD: MBBank, Vietcombank, Techcombank, ACB...)</summary>
    public string BankName { get; set; } = "MBBank";

    /// <summary>Tên chủ tài khoản ngân hàng nhận tiền (VD: CONG TY TNHH TRUSTSIM)</summary>
    public string AccountHolderName { get; set; } = string.Empty;

    /// <summary>Token xác thực Webhook gửi từ SePay (Authorization header: Apikey {token})</summary>
    public string WebhookToken { get; set; } = string.Empty;

    /// <summary>URL Webhook nhận thông báo biến động số dư từ SePay</summary>
    public string WebhookUrl { get; set; } = string.Empty;

    /// <summary>Template mẫu giao diện VietQR của SePay (VD: compact, compact2, qr_only)</summary>
    public string QrTemplate { get; set; } = "compact2";

    /// <summary>Tiền tố nhận diện mã chuyển khoản (VD: TS hoặc TRUSTSIM)</summary>
    public string PaymentPrefix { get; set; } = "TS";

    /// <summary>Thời gian hết hạn của giao dịch nạp tiền (phút)</summary>
    public int TransactionExpiryMinutes { get; set; } = 30;
}
