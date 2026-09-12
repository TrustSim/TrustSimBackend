using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Financial
{
    /// <summary>
    /// Lưu trữ lịch sử các giao dịch tài chính nội bộ phát sinh từ hoạt động đấu giá SIM
    /// (khóa cọc, hoàn cọc, giữ tiền ký quỹ Escrow, thanh toán cho người bán, phạt vi phạm...).
    /// Bất biến (Immutable) — chỉ thêm mới để làm bằng chứng kiểm toán tài chính, KHÔNG BAO GIỜ xóa.
    /// </summary>
    public class AuctionTransaction : AuditableEntity
    {

        // --- Foreign Key trỏ về Ví ---
        /// <summary>ID của ví tiền (<see cref="Wallet"/>) phát sinh giao dịch.</summary>
        public Guid WalletId { get; set; }

        // --- Trạng thái và loại giao dịch ---
        /// <summary>Trạng thái giao dịch: PENDING, COMPLETED, FAILED, CANCELLED.</summary>
        public TransactionStatus TransactionStatus { get; set; } = TransactionStatus.PENDING;

        /// <summary>
        /// Loại giao dịch tài chính đấu giá:
        /// AUCTION_DEPOSIT_LOCK, AUCTION_DEPOSIT_RELEASED, ESCROW_HOLD, SELLER_PAYOUT, PENALTY...
        /// </summary>
        public AuctionTransactionType AuctionTransactionType { get; set; }

        /// <summary>Số tiền giao dịch (VNĐ).</summary>
        public decimal Amount { get; set; }

        // --- Mốc thời gian ---
        /// <summary>Thời điểm khởi tạo giao dịch.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Thời điểm giao dịch hoàn tất thành công. Null nếu chưa hoàn tất.</summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>Thời điểm hết hạn hiệu lực của giao dịch.</summary>
        public DateTime? ExpiredAt { get; set; }

        // --- Dấu vết kiểm toán số dư (Audit Trail) ---
        /// <summary>Số dư ví trước khi thực hiện giao dịch này.</summary>
        public decimal BalanceBefore { get; set; }

        /// <summary>Số dư ví sau khi giao dịch hoàn tất thành công.</summary>
        public decimal BalanceAfter { get; set; }

        // --- Foreign Key liên kết nghiệp vụ (Nullable) ---
        /// <summary>ID ký quỹ liên quan (nếu giao dịch này nạp/khấu trừ tiền cho một Escrow) (N-1).</summary>
        public Guid? EscrowId { get; set; }

        // --- Navigation Properties ---
        /// <summary>Ví tiền liên kết với giao dịch đấu giá này.</summary>
        public virtual Wallet? Wallet { get; set; }

        /// <summary>Khoản ký quỹ liên kết với giao dịch này (nếu có) (N-1).</summary>
        public virtual Escrow? Escrow { get; set; }

        /// <summary>Tiền cọc liên quan đến giao dịch này (nếu là giao dịch khóa/hoàn cọc).</summary>
        public virtual AuctionDeposit? AuctionDeposit { get; set; }
    }
}
