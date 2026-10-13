using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Trade
{
    /// <summary>
    /// Đại diện cho một chữ ký số dùng trong giao dịch chuyển nhượng SIM.
    /// Lưu trữ thông tin về loại chữ ký số (hợp đồng, biên bản), dữ liệu được ký,
    /// và trạng thái xác thực.
    /// </summary>
    public class DigitalSignature: BaseEntity
    {

        /// <summary>
        /// Loại chữ ký / Vai trò của bên tham gia ký (BUYER, SELLER).
        /// </summary>
        public SignatureRole SignatureRole { get; set; }

        /// <summary>
        /// Phương thức xác thực ý chí: EMAIL_OTP, SMS_OTP, TRANSACTION_PIN, BIOMETRIC.
        /// </summary>
        public string AuthMethod { get; set; } = "EMAIL_OTP";

        /// <summary>
        /// Địa chỉ IP của thiết bị người dùng tại thời điểm bấm ký.
        /// </summary>
        public string IpAddress { get; set; } = string.Empty;

        /// <summary>
        /// Thông tin trình duyệt / hệ điều hành (User-Agent) của thiết bị thực hiện ký.
        /// </summary>
        public string UserAgent { get; set; } = string.Empty;

        /// <summary>
        /// Mã băm SHA-256 của mã OTP đã xác thực thành công.
        /// </summary>
        public string OtpCodeHash { get; set; } = string.Empty;

        /// <summary>
        /// Thời điểm xác thực mã OTP thành công (UTC).
        /// </summary>
        public DateTime OtpVerifiedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Dữ liệu ngữ cảnh giao dịch (Transaction Binding Metadata) dạng JSON.
        /// </summary>
        public string? AuditMetadataJson { get; set; }

        /// <summary>
        /// Khóa công khai của người ký (dành cho tùy chọn ký mở rộng PKI nếu có).
        /// </summary>
        public string PublicKey { get; set; } = string.Empty;

        /// <summary>
        /// Chữ ký số mật mã (nếu có).
        /// </summary>
        public string CryptographicSignature { get; set; } = string.Empty;

        /// <summary>
        /// Thời gian ký số.
        /// </summary>
        public DateTime SignedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Khóa ngoại tới <see cref="TransferContract"/>.
        /// </summary>
        public Guid TransferContractId { get; set; }

        /// <summary>
        /// Hợp đồng liên quan.
        /// </summary>
        public virtual TransferContract? TransferContract { get; set; }

        /// <summary>
        /// Khóa ngoại tới <see cref="UserAccount"/>.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Người dùng đã ký.
        /// </summary>
        public virtual UserAccount? User { get; set; }

    }
}