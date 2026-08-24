using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Auction
{
    /// <summary>
    /// Tiền cọc (deposit/collateral) tham gia đấu giá.
    /// Chỉ Audit, KHÔNG xóa — lưu vết lịch sử thanh toán và hoàn trả.
    /// </summary>
    public class AuctionDeposit : AuditableEntity
    {
        // --- Khóa chính ---
        /// <summary>Khóa chính của bản ghi cọc (alias cho <see cref="BaseEntity.Id"/>).</summary>
        public Guid AuctionDepositId { get => Id; set => Id = value; }

        // --- Liên kết tham gia ---
        /// <summary>ID người tham gia đấu giá (<see cref="AuctionParticipant"/>).</summary>
        public Guid ParticipantId { get; set; }
        /// <summary>
        /// mã cọc
        /// </summary>
        public string DepositCode{get;set;}= String.Empty;

        // --- Số tiền và tiền tệ ---
        /// <summary>Số tiền cọc đã thanh toán.</summary>
        public decimal DepositAmount { get; set; }

        /// <summary>
        /// trạng thái cọc
        /// </summary>
        public DepositStatus DepositStatus {get;set;}= DepositStatus.PENDING;

        /// <summary>Tiền tệ của khoản cọc (VD: VND, USD).</summary>
        public string Currency { get; set; } = "VND";

        /// <summary>
        /// Thời gian tiền cọc được áp dụng vào ký quỹ
        /// </summary>
        public DateTime? AppliedtoEscrowedAt {get;set;}
        
        /// <summary>
        /// Thời gian tiền cọc được hoàn trả
        /// </summary>
        public DateTime? ReleasedAT {get;set;}
        
        /// <summary>
        /// Thời gian tiền cọc bị tịch thu
        /// </summary>
        public DateTime? ForfeitedAt {get;set;}

        /// <summary>
        /// Lý do tịch thu tiền cọc
        /// </summary>
        public  string? ForfeitedReason {get;set;}

        /// <summary>
        /// người tham gia đấu giá
        /// </summary>
        public virtual AuctionParticipant AuctionParticipant {get;set;}=null!;

    }
}