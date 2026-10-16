using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Entities.Dispute;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Trade
{
    /// <summary>
    /// Đại diện cho một giao dịch chuyển nhượng SIM giữa người mua và người bán.
    /// Lưu trữ thông tin về trạng thái giao dịch, các mốc thời gian quan trọng,
    /// số tiền giao dịch, và liên kết đến phiên đấu giá gốc.
    /// </summary>
    public class TradeTransaction: AuditableEntity
    {
        
        /// <summary>
        /// Mã giao dịch duy nhất, dùng để hiển thị và tham chiếu.
        /// </summary>
        public string TradeTransactionCode {get;set;}= string.Empty;

        /// <summary>
        /// Trạng thái hiện tại của giao dịch.
        /// </summary>
        public TradeTransactionStatus TradeTransactionStatus {get;set;}

        /// <summary>
        /// Thời điểm hết hạn đóng tiền ký quỹ (null nếu đã nạp đủ trước khi tạo giao dịch).
        /// </summary>
        public DateTime? EscrowDeadline { get; set; }

        /// <summary>
        /// Thời điểm hết hạn ký hợp đồng số (kích hoạt ở giai đoạn PendingSignature).
        /// </summary>
        public DateTime? SigningDeadline { get; set; }

        /// <summary>
        /// Thời điểm hết hạn chuyển nhượng SIM tại nhà mạng (chỉ kích hoạt sau khi ký xong HĐ số 2).
        /// </summary>
        public DateTime? TransferDeadline { get; set; }

        /// <summary>
        /// Thời điểm hết hạn nghiệm thu và thanh lý hợp đồng (chỉ kích hoạt sau khi nộp biên bản sang tên).
        /// </summary>
        public DateTime? LiquidationDeadline { get; set; }

        /// <summary>
        /// Thời điểm hoàn tất giao dịch (null khi đang trong tiến trình).
        /// </summary>
        public DateTime? CompletedAt { get; set; }
        
        /// <summary>
        /// Thời điểm hủy giao dịch (null nếu không bị hủy).
        /// </summary>
        public DateTime? CancelledAt { get; set; }
        
        /// <summary>
        /// Lý do hủy giao dịch.
        /// </summary>
        public string? CancelledReason {get;set;}

        /// <summary>
        /// Khóa ngoại liên kết đến phiên đấu giá gốc.
        /// </summary>
        public Guid AuctionId {get;set;}

        /// <summary>
        /// Phiên đấu giá liên quan.
        /// </summary>
        public virtual Auction? Auction { get; set; }

        /// <summary>
        /// Khóa ngoại liên kết đến người mua.
        /// </summary>
        public Guid BuyerId { get; set; }

        /// <summary>
        /// Người mua liên quan.
        /// </summary>
        public virtual UserAccount? Buyer { get; set; }

        /// <summary>
        /// Khóa ngoại liên kết đến người bán.
        /// </summary>
        public Guid SellerId { get; set; }

        /// <summary>
        /// Người bán liên quan.
        /// </summary>
        public virtual UserAccount? Seller { get; set; }

        /// <summary>
        /// Khóa ngoại liên kết đến SIM.
        /// </summary>
        public Guid SimNumberId { get; set; }

        /// <summary>
        /// SIM liên quan.
        /// </summary>
        public virtual SimNumber? SimNumber { get; set; }

        /// <summary>
        /// Quỹ ký quỹ liên quan.
        /// </summary>
        public virtual Escrow? Escrow { get; set; }

        /// <summary>
        /// Hợp đồng chuyển nhượng liên quan.
        /// </summary>
        public virtual TransferContract? TransferContract { get; set; }

        /// <summary>
        /// Danh sách phiếu kiện liên quan.
        /// </summary>
        private readonly List<DisputeTicket> _disputeTickets = new();
        public IReadOnlyCollection<DisputeTicket> DisputeTickets => _disputeTickets.AsReadOnly();

        /// <summary>
        /// Chuyển nhượng SIM liên quan.
        /// </summary>
        public virtual SimTransfer? SimTransfer { get; set; }
    }
}