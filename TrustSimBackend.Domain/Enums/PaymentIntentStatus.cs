namespace TrustSimBackend.Domain.Enums;

/// <summary>
/// Trạng thái của Payment Intent theo State Machine.
/// </summary>
public enum PaymentIntentStatus
{
    /// <summary>Yêu cầu thanh toán vừa được tạo.</summary>
    CREATED = 1,

    /// <summary>Đang chờ xử lý / Người dùng đang quét QR hoặc Payout đang gửi qua ngân hàng.</summary>
    PROCESSING = 2,

    /// <summary>Thanh toán thành công / Đã ghi sổ cái và cập nhật ví.</summary>
    SUCCEEDED = 3,

    /// <summary>Thanh toán thất bại do ngân hàng từ chối hoặc lỗi xử lý.</summary>
    FAILED = 4,

    /// <summary>Hết hạn thời gian thanh toán (15 phút).</summary>
    EXPIRED = 5,

    /// <summary>Giao dịch đã bị người dùng hoặc hệ thống hủy.</summary>
    CANCELLED = 6
}
