using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Auctions
{
    /// <summary>
    /// Tiền cọc (Deposit/Collateral) bắt buộc của người dùng để đăng ký tham gia đấu giá.
    /// Chỉ Audit, KHÔNG xóa — lưu vết lịch sử thanh toán, hoàn trả hoặc tịch thu.
    /// </summary>
    public class AuctionDeposit : AuditableEntity
    {
        /// <summary>Khóa chính của bản ghi cọc (alias cho <see cref="BaseEntity.Id"/>).</summary>
        public Guid AuctionDepositId { get => Id; set => Id = value; }

        // --- Liên kết người tham gia ---
        /// <summary>ID người tham gia đấu giá (<see cref="AuctionParticipant"/>) nộp khoản cọc này.</summary>
        public Guid ParticipantId { get; set; }

        /// <summary>Mã định danh của khoản cọc (VD: "DEP-20240801-001").</summary>
        public string DepositCode { get; set; } = string.Empty;

        /// <summary>Số tiền cọc (VNĐ).</summary>
        public decimal DepositAmount { get; set; }

        /// <summary>Trạng thái cọc: PENDING, LOCKED, RELEASED, APPLIED_TO_ESCROW, FORFEITED, FAILED.</summary>
        public DepositStatus DepositStatus { get; set; } = DepositStatus.PENDING;

        /// <summary>Thời điểm tiền cọc được chuyển đổi áp dụng vào quỹ ký quỹ Escrow (nếu người này trúng đấu giá).</summary>
        public DateTime? AppliedtoEscrowedAt { get; set; }

        /// <summary>Thời điểm hoàn trả tiền cọc về ví (nếu không trúng đấu giá).</summary>
        public DateTime? ReleasedAT { get; set; }

        /// <summary>Thời điểm tiền cọc bị tịch thu do vi phạm quy chế đấu giá.</summary>
        public DateTime? ForfeitedAt { get; set; }

        /// <summary>Lý do tịch thu tiền cọc.</summary>
        public string? ForfeitedReason { get; set; }

        // --- Foreign Key trỏ về giao dịch khóa tiền cọc ---
        /// <summary>ID của giao dịch tài chính thực hiện việc khóa tiền cọc trong ví.</summary>
        public Guid AuctionTransactionId { get; set; }

        // --- Navigation Properties ---
        /// <summary>Người tham gia đấu giá nộp khoản cọc này.</summary>
        public virtual AuctionParticipant? AuctionParticipant { get; set; }

        /// <summary>Giao dịch tài chính tương ứng với lần khóa/hoàn cọc này.</summary>
        public virtual AuctionTransaction? AuctionTransaction { get; set; }

        /// <summary>Khoản ký quỹ Escrow mà tiền cọc này được chuyển vào (nếu có).</summary>
        public virtual Escrow? Escrow { get; set; }
    }
}