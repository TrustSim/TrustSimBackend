using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Financial
{
    /// <summary>
    /// Lưu trữ lịch sử giao dịch nạp / rút tiền qua ngân hàng (Banking Gateway / SePay).
    /// Bất biến (Immutable) — chỉ thêm mới và cập nhật trạng thái kết quả từ ngân hàng, KHÔNG BAO GIỜ xóa.
    /// </summary>
    public class WalletTransaction : AuditableEntity
    {

        // --- Khóa ngoại ---
        /// <summary>ID của ví tiền (<see cref="Wallet"/>) thực hiện giao dịch.</summary>
        public Guid WalletId { get; set; }

        /// <summary>
        /// Mã giao dịch nội bộ duy nhất dùng để đối soát và làm nội dung chuyển khoản (VD: "TS982312").
        /// </summary>
        public string TransactionCode { get; set; } = string.Empty;

        /// <summary>
        /// Mã giao dịch hoặc mã tham chiếu từ ngân hàng / SePay (ID giao dịch SePay / Reference Code).
        /// </summary>
        public string? ExternalTransactionId { get; set; }

        /// <summary>
        /// URL ảnh mã QR VietQR sinh từ cổng thanh toán SePay.
        /// </summary>
        public string? QrCodeUrl { get; set; }

        /// <summary>
        /// Ghi chú, nội dung giao dịch hoặc lý do từ chối rút tiền từ Admin.
        /// </summary>
        public string? Description { get; set; }

        // --- Trạng thái và loại giao dịch ---
        /// <summary>Trạng thái giao dịch: PENDING, COMPLETED, FAILED, CANCELLED.</summary>
        public TransactionStatus TransactionStatus { get; set; } = TransactionStatus.PENDING;

        /// <summary>Loại giao dịch ví: TOP_UP (Nạp tiền), WITHDRAW (Rút tiền).</summary>
        public WalletTransactionType WalletTransactionType { get; set; } = WalletTransactionType.TOP_UP;

        /// <summary>Số tiền giao dịch (VNĐ).</summary>
        public decimal Amount { get; set; }

        // --- Mốc thời gian ---
        /// <summary>Thời điểm tạo yêu cầu giao dịch.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Thời điểm giao dịch hoàn tất (ngân hàng xác nhận thành công). Null nếu chưa hoàn tất.</summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>Thời điểm hết hạn thanh toán (timeout nếu người dùng không thanh toán).</summary>
        public DateTime? ExpiredAt { get; set; }

        // --- Thông tin tài khoản ngân hàng đích (dùng khi RÚT TIỀN) ---
        /// <summary>Mã ngân hàng nhận tiền (VD: "VCB", "TCB", "MB", "970436").</summary>
        public string DestinationBankCode { get; set; } = string.Empty;

        /// <summary>Tên đầy đủ của ngân hàng nhận tiền (VD: "Vietcombank").</summary>
        public string DestinationBankName { get; set; } = string.Empty;

        /// <summary>Số tài khoản thực tế của người dùng tại ngân hàng.</summary>
        public string DestinationAccountNumber { get; set; } = string.Empty;

        /// <summary>Số tài khoản đã được che bớt để hiển thị an toàn trên giao diện (VD: "****1234").</summary>
        public string DestinationAccountNumberMasked { get; set; } = string.Empty;

        /// <summary>Tên chủ tài khoản nhận tiền tại ngân hàng (xác thực từ Bank API).</summary>
        public string DestinationAccountHolderName { get; set; } = string.Empty;

        // --- Dấu vết kiểm toán số dư (Audit Trail) ---
        /// <summary>Số dư ví trước khi thực hiện giao dịch này.</summary>
        public decimal BalanceBefore { get; set; }

        /// <summary>Số dư ví sau khi giao dịch hoàn tất thành công.</summary>
        public decimal BalanceAfter { get; set; }

        // --- Navigation Property ---
        /// <summary>Ví tiền liên kết với giao dịch này.</summary>
        public virtual Wallet? Wallet { get; set; }
    }
}
