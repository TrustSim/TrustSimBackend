using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Financial
{
    /// <summary>
    /// Lưu trữ lịch sử giao dịch nạp / rút tiền qua ngân hàng (Banking Gateway).
    /// Bất biến (Immutable) — chỉ thêm mới và cập nhật trạng thái kết quả từ ngân hàng, KHÔNG BAO GIỜ xóa.
    /// </summary>
    public class WalletTransaction : AuditableEntity
    {
        /// <summary>Khóa chính của giao dịch nạp/rút (alias cho <see cref="BaseEntity.Id"/>).</summary>
        public Guid WalletTransactionId { get => Id; private set => Id = value; }

        // --- Khóa ngoại ---
        /// <summary>ID của ví tiền (<see cref="Wallet"/>) thực hiện giao dịch.</summary>
        public Guid WalletId { get; set; }

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
