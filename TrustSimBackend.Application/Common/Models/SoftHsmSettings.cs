namespace TrustSimBackend.Application.Common.Models;

/// <summary>
/// Cấu hình kết nối và vận hành dịch vụ phần cứng bảo mật ảo Universal SoftHSMv2 Microservice.
/// </summary>
public class SoftHsmSettings
{
    public const string SectionName = "SoftHsm";

    /// <summary>Địa chỉ gốc của dịch vụ SoftHSM (ví dụ: https://universal-crypto-softhsm.onrender.com)</summary>
    public string BaseUrl { get; set; } = "https://universal-crypto-softhsm.onrender.com";

    /// <summary>Khóa API xác thực (truyền qua header X-API-Key)</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Nhãn cặp khóa Master ECDSA dùng cho sổ cái tài chính</summary>
    public string FinancialKeyLabel { get; set; } = "trustsim:financial:master-ledger-ecdsa";

    /// <summary>Nhãn cặp khóa Master RSA dùng cho ký số hợp đồng</summary>
    public string ContractKeyLabel { get; set; } = "trustsim:contract:master-signing-key";

    /// <summary>Nhãn cặp khóa Master RSA dùng cho mã hóa phong bì (Envelope Encryption) bảo vệ eKYC</summary>
    public string EnvelopeKeyLabel { get; set; } = "trustsim:system:envelope-master-key";

    /// <summary>Thời gian chờ tối đa cho mỗi request HTTP (giây)</summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>Số lần thử lại khi gặp lỗi tạm thời (Transient Error)</summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>Cờ kích hoạt sử dụng dịch vụ SoftHSM Cloud từ xa (nếu false sẽ rơi về Mock cục bộ)</summary>
    public bool EnableRemoteHsm { get; set; } = true;

    /// <summary>Cờ bật tiến trình chạy nền định kỳ ping /health giữ container Render luôn thức</summary>
    public bool EnableKeepAlive { get; set; } = true;

    /// <summary>Cờ bật chiến lược Xác thực Lai (nạp Public Key PEM vào cache và đối soát offline trong RAM)</summary>
    public bool UseHybridVerification { get; set; } = true;
}
