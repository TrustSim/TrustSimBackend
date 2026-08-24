using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Financial
{
    /// <summary>
    /// Quản lý khoản tiền ký quỹ (Escrow) của người thắng đấu giá trước khi chuyển nhượng SIM hoàn tất.
    /// Một Escrow có thể nhận nhiều đợt nạp/giao dịch tài chính (quan hệ 1 - N với <see cref="AuctionTransaction"/>).
    /// </summary>
    public class Escrow : AuditableEntity
    {
        /// <summary>Khóa chính của khoản ký quỹ (alias cho <see cref="BaseEntity.Id"/>).</summary>
        public Guid EscrowId { get => Id; private set => Id = value; }

        /// <summary>Tổng số tiền mục tiêu cần ký quỹ (hoặc số tiền đã ký quỹ thành công) (VNĐ).</summary>
        public decimal EscrowAmount { get; set; }

        /// <summary>Trạng thái ký quỹ: PENDING_FUNDING, HELD, FROZEN, RELEASED, REFUNDED, CANCELLED.</summary>
        public EscrowStatus EscrowStatus { get; set; } = EscrowStatus.PENDING_FUNDING;

        /// <summary>Thời điểm bắt đầu giữ tiền ký quỹ.</summary>
        public DateTime HeldAt { get; set; } = DateTime.UtcNow;

        /// <summary>Thời điểm giải ngân tiền ký quỹ cho người bán (sau khi hoàn tất chuyển nhượng).</summary>
        public DateTime? ReleasedAt { get; set; }

        /// <summary>Thời điểm hoàn trả tiền ký quỹ cho người mua (nếu giao dịch/hợp đồng bị hủy).</summary>
        public DateTime? RefundedAt { get; set; }

        /// <summary>Thời điểm đóng băng tiền ký quỹ (nếu có tranh chấp phát sinh).</summary>
        public DateTime? FrozenAt { get; set; }

        /// <summary>Lý do đóng băng tiền ký quỹ.</summary>
        public string? FrozenReason { get; set; }

        // --- Foreign Key ---
        /// <summary>Tiền cọc ban đầu được chuyển đổi sang làm một phần của quỹ Escrow (nếu có).</summary>
        public Guid? AuctionDepositId { get; set; }

        // --- Navigation Properties ---
        /// <summary>Tiền cọc ban đầu liên kết với quỹ Escrow này (nếu có).</summary>
        public virtual AuctionDeposit? AuctionDeposit { get; set; }

        /// <summary>
        /// Danh sách các giao dịch tài chính nạp/khấu trừ tiền vào khoản ký quỹ này (quan hệ 1 - N).
        /// Cho phép người dùng nạp tiền vào Escrow thành nhiều đợt.
        /// </summary>
        public virtual ICollection<AuctionTransaction> AuctionTransactions { get; set; } = new HashSet<AuctionTransaction>();
    }
}