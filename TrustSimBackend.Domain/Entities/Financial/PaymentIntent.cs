using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Financial;

/// <summary>
/// Quản lý ý định thanh toán tài chính (Payment Intent) theo State Machine &amp; Idempotent.
/// Theo dõi toàn bộ vòng đời từ khi phát sinh yêu cầu nạp/rút đến khi tiền được ghi sổ thành công.
/// </summary>
public class PaymentIntent : AuditableEntity
{
    /// <summary>
    /// Mã ý định thanh toán duy nhất (VD: "PI-20260919-8172").
    /// </summary>
    public string IntentCode { get; set; } = string.Empty;

    /// <summary>
    /// ID tài khoản người dùng khởi tạo yêu cầu.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// ID ví tiền của người dùng thực hiện giao dịch.
    /// </summary>
    public Guid WalletId { get; set; }

    /// <summary>
    /// Loại ý định thanh toán: DEPOSIT, WITHDRAW, ESCROW_PAYMENT.
    /// </summary>
    public PaymentIntentType IntentType { get; set; }

    /// <summary>
    /// Trạng thái của Payment Intent: CREATED, PROCESSING, SUCCEEDED, FAILED, EXPIRED, CANCELLED.
    /// </summary>
    public PaymentIntentStatus Status { get; set; } = PaymentIntentStatus.CREATED;

    /// <summary>
    /// Số tiền giao dịch (VNĐ).
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Đơn vị tiền tệ (mặc định "VND").
    /// </summary>
    public string Currency { get; set; } = "VND";

    /// <summary>
    /// Khóa Idempotency Key từ Client hoặc hệ thống sinh ra để chống lặp request.
    /// </summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>
    /// Cổng thanh toán áp dụng (VD: "SePay", "VietQR", "MockBank").
    /// </summary>
    public string GatewayProvider { get; set; } = "SePay";

    /// <summary>
    /// Mã giao dịch tham chiếu từ cổng thanh toán bên ngoài (nếu có).
    /// </summary>
    public string? ExternalTransactionId { get; set; }

    /// <summary>
    /// Đường dẫn ảnh mã VietQR phục vụ thanh toán (nếu là nạp tiền).
    /// </summary>
    public string? QrCodeUrl { get; set; }

    /// <summary>
    /// Chi tiết bổ sung (thông tin ngân hàng nhận khi rút tiền, ghi chú thanh toán,...).
    /// </summary>
    public string? PaymentDetails { get; set; }

    /// <summary>
    /// Thời điểm hết hạn thanh toán (mặc định 15 phút cho lệnh nạp).
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Thời điểm thanh toán hoàn tất thành công.
    /// </summary>
    public DateTime? SucceededAt { get; set; }

    /// <summary>
    /// Lý do thất bại nếu thanh toán bị từ chối/lỗi.
    /// </summary>
    public string? FailedReason { get; set; }

    // --- Navigation Properties ---
    /// <summary>
    /// Tài khoản người dùng sở hữu giao dịch này.
    /// </summary>
    public virtual UserAccount? UserAccount { get; set; }

    /// <summary>
    /// Ví tiền liên kết.
    /// </summary>
    public virtual Wallet? Wallet { get; set; }
}
